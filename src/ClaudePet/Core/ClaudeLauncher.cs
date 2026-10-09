using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace ClaudePet.Core;

/// <summary>Opens Claude Code in a given folder: in a console window or in the Claude Desktop app.</summary>
public static class ClaudeLauncher
{
    private const int MaxRecent = 10;

    /// <summary>Full path of claude.exe, or null if Claude Code is not installed.</summary>
    public static string? FindClaude() => FindClaude(UserEnvironment());

    public static bool HasWindowsTerminal => FindWindowsTerminal(UserEnvironment()) != null;

    private static string? FindClaude(Dictionary<string, string> env)
    {
        string native = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "bin", "claude.exe");
        return FindOnPath(env, "claude.exe") ?? FindOnPath(env, "claude.cmd") ?? (File.Exists(native) ? native : null);
    }

    /// <summary>
    /// Starts Claude Code in <paramref name="folder"/> and remembers the folder as recent project.
    /// Throws <see cref="FileNotFoundException"/> if Claude Code is missing. For the Desktop app it returns
    /// the short-lived <c>claude --desktop</c> process, whose exit code tells whether the hand-over worked.
    /// </summary>
    public static Process? Launch(AppSettings settings, string folder)
    {
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(folder);
        var env = UserEnvironment();
        string claude = FindClaude(env)
            ?? throw new FileNotFoundException(Strings.ClaudeNotFound);

        var terminal = settings.Terminal;
        string? wt = FindWindowsTerminal(env);
        if (terminal == TerminalKind.Auto) terminal = wt != null ? TerminalKind.WindowsTerminal : TerminalKind.Cmd;
        if (terminal == TerminalKind.WindowsTerminal && (wt == null || folder.Contains(';'))) terminal = TerminalKind.Cmd;

        var start = terminal switch
        {
            // Hands the folder over to the Desktop app and exits; needs a recent CLI and Desktop installed.
            TerminalKind.Desktop => new ProcessStartInfo(claude) { ArgumentList = { "--desktop" }, CreateNoWindow = true },
            // wt treats ";" as a command separator, so keep it out of the arguments.
            TerminalKind.WindowsTerminal => new ProcessStartInfo(wt!)
            {
                ArgumentList =
                {
                    "new-tab", "-d", folder, "--title", "Claudius – " + FolderName(folder).Replace(';', ','),
                    "cmd.exe", "/k", claude,
                },
            },
            TerminalKind.PowerShell => new ProcessStartInfo("powershell.exe")
            {
                ArgumentList = { "-NoExit", "-Command", "& '" + claude.Replace("'", "''") + "'" },
            },
            // The folder goes via WorkingDirectory, never into the command line: names may contain & or ^.
            _ => new ProcessStartInfo("cmd.exe") { Arguments = $"/k \"{claude}\"" },
        };
        start.WorkingDirectory = folder;
        // No shell execute, so the environment can be replaced. The pet is a GUI app without a console,
        // so cmd/PowerShell still get a console window of their own.
        start.UseShellExecute = false;
        start.Environment.Clear();
        foreach (var (name, value) in env) start.Environment[name] = value;

        Process? process;
        try
        {
            process = Process.Start(start);
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException((terminal == TerminalKind.Desktop
                ? Strings.DesktopOpenFailed : Strings.ConsoleStartFailed) + ex.Message, ex);
        }

        Remember(settings, folder);
        if (terminal == TerminalKind.Desktop) return process;
        process?.Dispose();
        return null;
    }

    public static string FolderName(string folder)
    {
        string trimmed = folder.TrimEnd('\\', '/');
        string name = Path.GetFileName(trimmed);
        return name.Length > 0 ? name : trimmed;
    }

    private static void Remember(AppSettings settings, string folder)
    {
        string full = Path.GetFullPath(folder).TrimEnd('\\');
        settings.RecentProjects.RemoveAll(p => string.Equals(p.TrimEnd('\\'), full, StringComparison.OrdinalIgnoreCase));
        settings.RecentProjects.Insert(0, full);
        if (settings.RecentProjects.Count > MaxRecent)
            settings.RecentProjects.RemoveRange(MaxRecent, settings.RecentProjects.Count - MaxRecent);
    }

    private static string? FindWindowsTerminal(Dictionary<string, string> env)
    {
        // wt.exe is an app execution alias; File.Exists sees it in WindowsApps.
        string alias = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WindowsApps", "wt.exe");
        return FindOnPath(env, "wt.exe") ?? (File.Exists(alias) ? alias : null);
    }

    private static string? FindOnPath(Dictionary<string, string> env, string file)
    {
        foreach (string dir in env.GetValueOrDefault("Path", "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            try
            {
                string candidate = Path.Combine(dir.Trim('"'), file);
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException) { }
        }
        return null;
    }

    /// <summary>
    /// A fresh environment built from the user's and the system's stored variables, as Explorer gives
    /// to programs it starts. The pet's own environment is not passed on: if the pet was started from
    /// inside Claude Code it carries that session's markers (Claude would think it is a child session)
    /// and e.g. NO_COLOR (the logo would lose its color). Also picks up PATH changes made since the pet started.
    /// </summary>
    private static Dictionary<string, string> UserEnvironment()
    {
        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        IntPtr token = IntPtr.Zero, block = IntPtr.Zero;
        try
        {
            if (OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY | TOKEN_DUPLICATE, out token)
                && CreateEnvironmentBlock(out block, token, false))
            {
                // "NAME=value\0NAME=value\0...\0\0" in UTF-16
                for (IntPtr p = block; ;)
                {
                    string? entry = Marshal.PtrToStringUni(p);
                    if (string.IsNullOrEmpty(entry)) break;
                    int eq = entry.IndexOf('=', 1);   // entries like "=C:=C:\" start with '='
                    if (eq > 0) env[entry[..eq]] = entry[(eq + 1)..];
                    p += (entry.Length + 1) * 2;
                }
                return env;
            }
            Log.Write("Benutzerumgebung nicht verfügbar, verwende die eigene: " + Marshal.GetLastWin32Error());
        }
        finally
        {
            if (block != IntPtr.Zero) DestroyEnvironmentBlock(block);
            if (token != IntPtr.Zero) CloseHandle(token);
        }

        foreach (System.Collections.DictionaryEntry e in Environment.GetEnvironmentVariables())
            env[(string)e.Key] = (string?)e.Value ?? "";
        return env;
    }

    private const uint TOKEN_DUPLICATE = 0x0002;
    private const uint TOKEN_QUERY = 0x0008;

    [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool CreateEnvironmentBlock(out IntPtr block, IntPtr token, bool inherit);
    [DllImport("userenv.dll")] private static extern bool DestroyEnvironmentBlock(IntPtr block);
}
