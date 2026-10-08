using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace ClaudePet.Core;

/// <summary>
/// Notices when the user dictates to Claude Code: voice dictation is on, a terminal or the Claude
/// Desktop app is in the foreground while Claude runs, and Space is held down (push to talk). Claude Code reports
/// nothing about this itself, so it is inferred from the keyboard; a short tap is just a space.
/// </summary>
public sealed class VoiceWatcher
{
    private static readonly TimeSpan HoldTime = TimeSpan.FromMilliseconds(350);
    private static readonly HashSet<string> Hosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "WindowsTerminal", "OpenConsole", "conhost", "cmd", "powershell", "pwsh",
        "Code", "Cursor", "wezterm-gui", "alacritty", "Hyper", "Tabby",
        "claude", // Claude Desktop (Code tab)
    };

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private DateTime? _spaceSince;
    private bool _candidate;

    /// <summary>True while the user is (probably) talking to Claude Code.</summary>
    public bool Listening { get; private set; }
    public event Action<bool>? ListeningChanged;

    public VoiceWatcher() => _timer.Tick += (_, _) => Poll();

    public void Start() => _timer.Start();

    private void Poll()
    {
        bool down = GetAsyncKeyState(VK_SPACE) < 0;
        if (!down)
        {
            _spaceSince = null;
            SetListening(false);
            return;
        }

        if (_spaceSince == null)
        {
            // Checked once per key press: cheap enough, and the focus does not change while holding Space.
            _spaceSince = DateTime.Now;
            _candidate = HostInForeground() && ClaudeRunning() && ClaudeCodeSetup.IsVoiceEnabled();
        }
        SetListening(_candidate && DateTime.Now - _spaceSince >= HoldTime);
    }

    private void SetListening(bool listening)
    {
        if (listening == Listening) return;
        Listening = listening;
        ListeningChanged?.Invoke(listening);
    }

    private static bool HostInForeground()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero) return false;
        GetWindowThreadProcessId(window, out int pid);
        try
        {
            using var process = Process.GetProcessById(pid);
            return Hosts.Contains(process.ProcessName);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool ClaudeRunning()
    {
        var processes = Process.GetProcessesByName("claude");
        foreach (var p in processes) p.Dispose();
        return processes.Length > 0;
    }

    private const int VK_SPACE = 0x20;

    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);
}
