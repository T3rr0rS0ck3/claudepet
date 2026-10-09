using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using Claudius.Shared;

namespace Claudius.Core;

/// <summary>
/// Brings the window of an assistant session to the front: the console or Windows Terminal window the
/// bridge found for it, in Windows Terminal also its tab, or the desktop app. Sessions saved
/// without a window (older bridge, or none found) are looked for by their folder name.
/// </summary>
public static class SessionWindow
{
    private const string TerminalClass = "CASCADIA_HOSTING_WINDOW_CLASS";
    private const string ConsoleClass = "ConsoleWindowClass";

    /// <summary>
    /// Runs in the background, as UI Automation may take a moment. Call it right on the user's click:
    /// the process that got the last input may take the foreground, later Windows refuses.
    /// </summary>
    public static Task<bool> ActivateAsync(SessionView session) => Task.Run(() =>
    {
        try
        {
            return Activate(session.Info, session.Folder, session.Desktop);
        }
        catch (Exception ex)
        {
            Log.Write("Session-Fenster konnte nicht geholt werden: " + ex);
            return false;
        }
    });

    /// <summary>False if no window was found.</summary>
    public static bool Activate(SessionInfo info, string folder, bool desktop)
    {
        var hints = Hints(info, folder);
        IntPtr window = Stored(info);
        if (window == IntPtr.Zero) window = desktop ? DesktopWindow() : FindByTitle(hints);
        if (window == IntPtr.Zero) return false;

        // The tab first, so the window does not show up with the wrong one; selecting works in the background.
        if (ClassName(window) == TerminalClass) FindTab(window, hints)?.Select();
        BringToFront(window);
        return true;
    }

    /// <summary>What a tab or window title of the session may contain, most specific first.</summary>
    private static List<string> Hints(SessionInfo info, string folder)
    {
        var hints = new List<string>();
        if (Clean(info.Title) is { Length: > 0 } title) hints.Add(title);
        if (folder.Length > 0 && !hints.Contains(folder, StringComparer.OrdinalIgnoreCase)) hints.Add(folder);
        return hints;
    }

    /// <summary>Without the leading status glyph (e.g. the assistant's spinner), which changes all the time.</summary>
    private static string Clean(string? title)
    {
        if (title == null) return "";
        int start = 0;
        while (start < title.Length && !char.IsLetterOrDigit(title[start])) start++;
        return title[start..].Trim();
    }

    /// <summary>The window the bridge stored, if it still exists and belongs to the same process.</summary>
    private static IntPtr Stored(SessionInfo info)
    {
        var window = new IntPtr(info.Window);
        if (info.Window == 0 || !IsWindow(window)) return IntPtr.Zero;
        GetWindowThreadProcessId(window, out uint pid);
        return pid == info.WindowPid ? window : IntPtr.Zero;
    }

    /// <summary>The desktop app's main window (its process shares the name with the CLI, which has none).</summary>
    private static IntPtr DesktopWindow()
    {
        var pids = new HashSet<uint>();
        foreach (var process in Process.GetProcessesByName(AssistantCli.Command))
            using (process) pids.Add((uint)process.Id);
        return TopLevelWindows().FirstOrDefault(w =>
        {
            GetWindowThreadProcessId(w, out uint pid);
            return pids.Contains(pid) && GetWindowTextLength(w) > 0;
        });
    }

    /// <summary>A console or Windows Terminal window titled after the session, or a Windows Terminal window with such a tab.</summary>
    private static IntPtr FindByTitle(List<string> hints)
    {
        var terminals = TopLevelWindows().Where(w => ClassName(w) is TerminalClass or ConsoleClass).ToList();
        foreach (string hint in hints)
        {
            var titled = terminals.FirstOrDefault(w => Clean(WindowTitle(w)).Contains(hint, StringComparison.OrdinalIgnoreCase));
            if (titled != IntPtr.Zero) return titled;
        }
        return terminals.FirstOrDefault(w => ClassName(w) == TerminalClass && FindTab(w, hints) != null);
    }

    /// <summary>The only tab whose title contains a hint, trying the hints in order; null if none or ambiguous.</summary>
    private static SelectionItemPattern? FindTab(IntPtr window, List<string> hints)
    {
        try
        {
            var tabs = AutomationElement.FromHandle(window).FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem));
            var named = tabs.Cast<AutomationElement>().Select(t => (Tab: t, Name: Clean(t.Current.Name))).ToList();
            foreach (string hint in hints)
            {
                var matches = named.Where(t => t.Name.Contains(hint, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count == 1 && matches[0].Tab.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var pattern))
                    return (SelectionItemPattern)pattern;
            }
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or COMException)
        {
            Log.Write("Windows-Terminal-Tabs nicht lesbar: " + ex.Message);
        }
        return null;
    }

    /// <summary>Restores and activates the window; falls back to sharing the foreground thread's input state.</summary>
    private static void BringToFront(IntPtr window)
    {
        if (IsIconic(window)) ShowWindow(window, SW_RESTORE);
        if (SetForegroundWindow(window)) return;

        uint foreground = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        uint me = GetCurrentThreadId();
        bool attached = foreground != 0 && foreground != me && AttachThreadInput(me, foreground, true);
        try
        {
            BringWindowToTop(window);
            SetForegroundWindow(window);
        }
        finally
        {
            if (attached) AttachThreadInput(me, foreground, false);
        }
    }

    /// <summary>Visible, unowned top-level windows of other processes, front to back.</summary>
    private static List<IntPtr> TopLevelWindows()
    {
        var windows = new List<IntPtr>();
        uint self = (uint)Environment.ProcessId;
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid != self && IsWindowVisible(hwnd) && GetWindow(hwnd, GW_OWNER) == IntPtr.Zero) windows.Add(hwnd);
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    private static string ClassName(IntPtr window)
    {
        var buffer = new StringBuilder(256);
        return GetClassName(window, buffer, buffer.Capacity) > 0 ? buffer.ToString() : "";
    }

    private static string WindowTitle(IntPtr window)
    {
        var buffer = new StringBuilder(GetWindowTextLength(window) + 1);
        return GetWindowText(window, buffer, buffer.Capacity) > 0 ? buffer.ToString() : "";
    }

    private const int SW_RESTORE = 9;
    private const uint GW_OWNER = 4;

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int cmd);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint attach, uint attachTo, bool on);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int size);
    [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int size);
    [DllImport("user32.dll", EntryPoint = "GetWindowTextLengthW")] private static extern int GetWindowTextLength(IntPtr hwnd);
}
