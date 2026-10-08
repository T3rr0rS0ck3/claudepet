using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ClaudePet.Bridge;

/// <summary>
/// Finds the window that shows the Claude Code session running this hook, so the pet can bring it to
/// the front. Hooks run as children of Claude Code (maybe via a shell of their own), Claude Code runs in
/// the user's shell, and that shell lives in a console window, a Windows Terminal tab or the Desktop app.
/// </summary>
internal static class SessionHost
{
    public readonly record struct Host(IntPtr Window, int Pid, string? Title);

    public static Host Find()
    {
        var chain = Ancestors();

        // The nearest console with a window: a classic console window itself, or for a Windows Terminal tab
        // the hidden pseudo console, which the terminal window owns. Hook shells started without a window
        // have consoles of their own without one and are skipped.
        FreeConsole();
        foreach (var (pid, _) in chain)
        {
            if (!AttachConsole((uint)pid)) continue;
            IntPtr console = GetConsoleWindow();
            string? title = ConsoleTitle();
            FreeConsole();
            if (console == IntPtr.Zero) continue;
            IntPtr window = GetAncestor(console, GA_ROOTOWNER);
            if (window == IntPtr.Zero) window = console;
            // A pseudo console counts as visible but has no size; without an owner it is no use.
            if (IsWindowVisible(window) && ClassName(window) != "PseudoConsoleWindow") return Make(window, title);
        }

        // No console window (the Desktop app, or a terminal that does not own its pseudo console):
        // the nearest ancestor with a window of its own. Explorer and the pet only started the session.
        foreach (var (pid, name) in chain)
        {
            if (name.StartsWith("explorer", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("ClaudePet", StringComparison.OrdinalIgnoreCase)) break;
            IntPtr window = MainWindow(pid);
            if (window != IntPtr.Zero) return Make(window, null);
        }
        return default;
    }

    private static Host Make(IntPtr window, string? title)
    {
        GetWindowThreadProcessId(window, out uint pid);
        return new Host(window, (int)pid, string.IsNullOrWhiteSpace(title) ? null : title);
    }

    /// <summary>Parent processes, nearest first. Stops where a parent id was reused by a younger process.</summary>
    private static List<(int Pid, string Name)> Ancestors()
    {
        var parents = new Dictionary<int, (int Parent, string Name)>();
        IntPtr snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == new IntPtr(-1)) return [];
        try
        {
            var entry = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
            for (bool ok = Process32First(snapshot, ref entry); ok; ok = Process32Next(snapshot, ref entry))
                parents[(int)entry.th32ProcessID] = ((int)entry.th32ParentProcessID, entry.szExeFile);
        }
        finally
        {
            CloseHandle(snapshot);
        }

        var chain = new List<(int, string)>();
        int current = Environment.ProcessId;
        DateTime started = DateTime.MaxValue;
        while (chain.Count < 16 && parents.TryGetValue(current, out var me) && parents.TryGetValue(me.Parent, out var parent))
        {
            DateTime parentStarted;
            try
            {
                using var process = Process.GetProcessById(me.Parent);
                parentStarted = process.StartTime;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                break;
            }
            if (parentStarted > started) break;
            chain.Add((me.Parent, parent.Name));
            current = me.Parent;
            started = parentStarted;
        }
        return chain;
    }

    /// <summary>The topmost visible, titled, unowned window of a process.</summary>
    private static IntPtr MainWindow(int pid)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out uint owner);
            if (owner != pid || !IsWindowVisible(hwnd) || GetWindow(hwnd, GW_OWNER) != IntPtr.Zero
                || GetWindowTextLength(hwnd) == 0) return true;
            found = hwnd;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    private static string ClassName(IntPtr window)
    {
        var buffer = new StringBuilder(256);
        return GetClassName(window, buffer, buffer.Capacity) > 0 ? buffer.ToString() : "";
    }

    private static string? ConsoleTitle()
    {
        var buffer = new StringBuilder(1024);
        return GetConsoleTitle(buffer, buffer.Capacity) > 0 ? buffer.ToString() : null;
    }

    private const uint TH32CS_SNAPPROCESS = 0x00000002;
    private const uint GA_ROOTOWNER = 3;
    private const uint GW_OWNER = 4;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32
    {
        public uint dwSize, cntUsage, th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID, cntThreads, th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode)]
    private static extern bool Process32First(IntPtr snapshot, ref PROCESSENTRY32 entry);
    [DllImport("kernel32.dll", EntryPoint = "Process32NextW", CharSet = CharSet.Unicode)]
    private static extern bool Process32Next(IntPtr snapshot, ref PROCESSENTRY32 entry);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll")] private static extern bool FreeConsole();
    [DllImport("kernel32.dll")] private static extern bool AttachConsole(uint pid);
    [DllImport("kernel32.dll")] private static extern IntPtr GetConsoleWindow();
    [DllImport("kernel32.dll", EntryPoint = "GetConsoleTitleW", CharSet = CharSet.Unicode)]
    private static extern int GetConsoleTitle(StringBuilder title, int size);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll", EntryPoint = "GetWindowTextLengthW")] private static extern int GetWindowTextLength(IntPtr hwnd);
    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int size);
}
