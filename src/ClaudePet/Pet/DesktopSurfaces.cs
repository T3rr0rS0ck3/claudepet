using System.Runtime.InteropServices;
using System.Text;

namespace ClaudePet.Pet;

/// <summary>Rectangle in physical screen pixels.</summary>
public readonly record struct ScreenRect(int Left, int Top, int Right, int Bottom)
{
    public bool Contains(double x, double y) => x >= Left && x < Right && y >= Top && y < Bottom;
    public bool Covers(ScreenRect other) =>
        Left <= other.Left && Top <= other.Top && Right >= other.Right && Bottom >= other.Bottom;
}

/// <summary>A horizontal edge the pet can stand on, in physical screen pixels.</summary>
public readonly record struct Platform(double Y, double X1, double X2, IntPtr Window)
{
    public bool IsFloor => Window == IntPtr.Zero;
    public bool Contains(double x) => x >= X1 && x <= X2;
}

/// <summary>
/// Snapshot of everything the pet can walk on: the bottom of each monitor's work area (the
/// taskbar) and the visible parts of the top edges of open windows.
/// </summary>
public sealed class DesktopSurfaces
{
    private static readonly int OwnProcess = Environment.ProcessId;
    private static readonly HashSet<string> ShellClasses =
        ["Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd"];

    private readonly Dictionary<IntPtr, ScreenRect> _windowRects = new();
    private readonly List<ScreenRect> _fullscreen = [];

    public List<Platform> Platforms { get; } = [];
    public List<ScreenRect> WorkAreas { get; } = [];
    public List<ScreenRect> Monitors { get; } = [];
    /// <summary>Incremented on every scan.</summary>
    public int Version { get; private set; }

    public void Scan(double petWidth, double petHeight)
    {
        Platforms.Clear();
        WorkAreas.Clear();
        Monitors.Clear();
        _windowRects.Clear();
        _fullscreen.Clear();
        Version++;

        MonitorEnumProc monitorProc = (IntPtr monitor, IntPtr _, ref RECT _, IntPtr _) =>
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(monitor, ref info))
            {
                Monitors.Add(info.rcMonitor.ToScreenRect());
                WorkAreas.Add(info.rcWork.ToScreenRect());
            }
            return true;
        };
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, monitorProc, IntPtr.Zero);

        foreach (var wa in WorkAreas)
            Platforms.Add(new Platform(wa.Bottom, wa.Left, wa.Right, IntPtr.Zero));

        // EnumWindows walks the z-order from top to bottom, so every window seen earlier
        // lies above the current one and may hide parts of its top edge.
        var windows = new List<IntPtr>();
        EnumWindowsProc windowProc = (hwnd, _) => { windows.Add(hwnd); return true; };
        EnumWindows(windowProc, IntPtr.Zero);

        var above = new List<ScreenRect>();
        foreach (var hwnd in windows)
        {
            if (!IsCandidate(hwnd) || LiveBounds(hwnd) is not { } rect) continue;
            if (rect.Right - rect.Left < 1 || rect.Bottom - rect.Top < 1) continue;
            _windowRects[hwnd] = rect;

            bool fillsMonitor = IsZoomed(hwnd) || Monitors.Any(m => rect.Covers(m));
            if (!fillsMonitor) AddTopEdge(hwnd, rect, above, petWidth, petHeight);
            above.Add(rect);
        }

        var foreground = GetForegroundWindow();
        if (foreground != IntPtr.Zero && _windowRects.TryGetValue(foreground, out var fg))
            _fullscreen.AddRange(Monitors.Where(m => fg.Covers(m)));
    }

    private void AddTopEdge(IntPtr hwnd, ScreenRect rect, List<ScreenRect> above, double petWidth, double petHeight)
    {
        double y = rect.Top;
        var pieces = new List<(double X1, double X2)> { (rect.Left, rect.Right) };
        foreach (var other in above)
            if (other.Top <= y && other.Bottom > y) Subtract(pieces, other.Left, other.Right);

        // Only where the pet fits on screen above the edge, and never off the side of a monitor.
        foreach (var wa in WorkAreas)
        {
            if (y < wa.Top + petHeight * 0.8 || y >= wa.Bottom - petHeight * 0.3) continue;
            foreach (var (x1, x2) in pieces)
            {
                double left = Math.Max(x1, wa.Left), right = Math.Min(x2, wa.Right);
                if (right - left >= petWidth * 0.6) Platforms.Add(new Platform(y, left, right, hwnd));
            }
        }
    }

    private static void Subtract(List<(double X1, double X2)> pieces, double left, double right)
    {
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            var (x1, x2) = pieces[i];
            if (right <= x1 || left >= x2) continue;
            pieces.RemoveAt(i);
            if (x1 < left) pieces.Add((x1, left));
            if (right < x2) pieces.Add((right, x2));
        }
    }

    /// <summary>Rectangle of the window as it was during the last scan.</summary>
    public ScreenRect? ScannedRect(IntPtr hwnd) => _windowRects.TryGetValue(hwnd, out var r) ? r : null;

    /// <summary>A fullscreen app is in the foreground on the monitor containing this point.</summary>
    public bool IsFullscreen(double x, double y) => _fullscreen.Any(m => m.Contains(x, y - 1));

    /// <summary>Current visible frame of a window, or null if it is gone, hidden or minimized.</summary>
    public static ScreenRect? LiveBounds(IntPtr hwnd)
    {
        if (!IsWindow(hwnd) || !IsWindowVisible(hwnd) || IsIconic(hwnd)) return null;
        if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return null;
        // The extended frame excludes the invisible resize borders around Windows 10/11 windows.
        if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT frame, Marshal.SizeOf<RECT>()) == 0)
            return frame.ToScreenRect();
        return GetWindowRect(hwnd, out var rect) ? rect.ToScreenRect() : null;
    }

    private static bool IsCandidate(IntPtr hwnd)
    {
        if (!IsWindowVisible(hwnd) || GetWindowTextLength(hwnd) == 0) return false;
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        if ((exStyle & (WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT)) != 0) return false;
        GetWindowThreadProcessId(hwnd, out int pid);
        if (pid == OwnProcess) return false;
        var name = new StringBuilder(64);
        GetClassName(hwnd, name, name.Capacity);
        return !ShellClasses.Contains(name.ToString());
    }

    // ---------------------------------------------------------------- Win32

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    private const int DWMWA_CLOAKED = 14;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
        public readonly ScreenRect ToScreenRect() => new(Left, Top, Right, Bottom);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, ref RECT rect, IntPtr data);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, StringBuilder name, int maxCount);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out RECT value, int size);
}
