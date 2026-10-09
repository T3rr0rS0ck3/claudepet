using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Claudius.Core;

/// <summary>
/// Finds the folder shown by the Explorer window (or desktop) under a screen point.
/// Coordinates are physical pixels; <c>ignore</c> is a window to look through (the ghost).
/// </summary>
public static class ExplorerLocator
{
    /// <summary>Whether the point is over Explorer or the desktop. Cheap (window classes only).</summary>
    public static bool IsShellAt(int x, int y, IntPtr ignore) => ShellWindowAt(x, y, ignore) != null;

    /// <summary>
    /// File system folder shown at the point, or null. <paramref name="isShell"/> tells whether the point
    /// was over Explorer/desktop at all (e.g. "This PC" is a shell window without a folder path).
    /// </summary>
    public static string? FolderAt(int x, int y, IntPtr ignore, out bool isShell)
    {
        var target = ShellWindowAt(x, y, ignore);
        isShell = target != null;
        if (target == null) return null;
        var (root, isDesktop) = target.Value;

        try
        {
            string? path = isDesktop
                ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
                : ExplorerPath(root);
            return path != null && Directory.Exists(path) ? path : null;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            Log.Write("Explorer-Ordner konnte nicht ermittelt werden: " + ex.Message);
            return null;
        }
    }

    private static (IntPtr Root, bool IsDesktop)? ShellWindowAt(int x, int y, IntPtr ignore)
    {
        var root = TopLevelWindowAt(x, y, ignore);
        if (root == IntPtr.Zero) return null;

        // Never treat our own windows (e.g. the pet itself) as a target.
        GetWindowThreadProcessId(root, out uint pid);
        if (pid == Environment.ProcessId) return null;

        return ClassName(root) switch
        {
            "CabinetWClass" or "ExploreWClass" => (root, false),
            "Progman" or "WorkerW" => (root, true),
            _ => null,
        };
    }

    private static IntPtr TopLevelWindowAt(int x, int y, IntPtr ignore)
    {
        var point = new POINT { X = x, Y = y };
        var root = GetAncestor(WindowFromPoint(point), GA_ROOT);
        if (root != ignore || ignore == IntPtr.Zero) return root;

        // The point hit the window to ignore: walk the z-order below it instead.
        for (var h = GetWindow(ignore, GW_HWNDNEXT); h != IntPtr.Zero; h = GetWindow(h, GW_HWNDNEXT))
        {
            if (!IsWindowVisible(h) || IsCloaked(h)) continue;
            if (GetWindowRect(h, out var r) && x >= r.Left && x < r.Right && y >= r.Top && y < r.Bottom) return h;
        }
        return IntPtr.Zero;
    }

    /// <summary>Path of the active tab of the Explorer window <paramref name="root"/>.</summary>
    private static string? ExplorerPath(IntPtr root)
    {
        var shellType = Type.GetTypeFromProgID("Shell.Application");
        if (shellType == null) return null;
        dynamic shell = Activator.CreateInstance(shellType)!;

        // Windows 11 tabs: every tab is its own entry with the same top-level HWND.
        // The active tab's ShellTabWindowClass comes first among the window's children.
        var activeTab = FindWindowEx(root, IntPtr.Zero, "ShellTabWindowClass", null);

        string? fallback = null;
        dynamic windows = shell.Windows();
        int count = windows.Count;
        for (int i = 0; i < count; i++)
        {
            object? item = windows.Item(i);
            if (item == null) continue;
            dynamic window = item;
            if ((IntPtr)(long)window.HWND != root) continue;
            string? path = window.Document?.Folder?.Self?.Path;
            if (activeTab == IntPtr.Zero || TabWindow(window) == activeTab) return path;
            fallback ??= path;
        }
        return fallback;
    }

    private static IntPtr TabWindow(object window)
    {
        try
        {
            if (window is not IComServiceProvider provider) return IntPtr.Zero;
            Guid service = SID_STopLevelBrowser, iid = typeof(IShellBrowser).GUID;
            if (provider.QueryService(ref service, ref iid, out var ptr) != 0 || ptr == IntPtr.Zero) return IntPtr.Zero;
            try
            {
                var browser = (IShellBrowser)Marshal.GetObjectForIUnknown(ptr);
                return browser.GetWindow(out var hwnd) == 0 ? hwnd : IntPtr.Zero;
            }
            finally
            {
                Marshal.Release(ptr);
            }
        }
        catch (COMException)
        {
            return IntPtr.Zero;
        }
    }

    private static string ClassName(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        return GetClassName(hwnd, sb, sb.Capacity) > 0 ? sb.ToString() : "";
    }

    private static bool IsCloaked(IntPtr hwnd) =>
        DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0;

    private static readonly Guid SID_STopLevelBrowser = new("4C96BE40-915C-11CF-99D3-00AA004AE837");

    [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IComServiceProvider
    {
        [PreserveSig] int QueryService(ref Guid guidService, ref Guid riid, out IntPtr ppvObject);
    }

    /// <summary>Only the first method (IOleWindow.GetWindow) is declared; nothing after it is called.</summary>
    [ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellBrowser
    {
        [PreserveSig] int GetWindow(out IntPtr phwnd);
    }

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }

    private const uint GA_ROOT = 2;
    private const uint GW_HWNDNEXT = 2;
    private const int DWMWA_CLOAKED = 14;

    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(POINT point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string? title);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out int value, int size);
}
