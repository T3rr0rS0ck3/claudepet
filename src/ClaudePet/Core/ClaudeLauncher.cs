using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace ClaudePet.Core;

/// <summary>Opens a console window running Claude Code in a given folder.</summary>
public static class ClaudeLauncher
{
    private const int MaxRecent = 10;

    /// <summary>Full path of claude.exe, or null if Claude Code is not installed.</summary>
    public static string? FindClaude()
    {
        string native = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "bin", "claude.exe");
        return FindOnPath("claude.exe") ?? FindOnPath("claude.cmd") ?? (File.Exists(native) ? native : null);
    }

    public static bool HasWindowsTerminal => FindWindowsTerminal() != null;

    /// <summary>
    /// Starts Claude Code in <paramref name="folder"/> and remembers the folder as recent project.
    /// Throws <see cref="FileNotFoundException"/> if Claude Code is missing.
    /// </summary>
    public static void Launch(AppSettings settings, string folder)
    {
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(folder);
        string claude = FindClaude()
            ?? throw new FileNotFoundException("Claude Code wurde nicht gefunden (claude ist nicht im PATH).");

        var terminal = settings.Terminal;
        string? wt = FindWindowsTerminal();
        if (terminal == TerminalKind.Auto) terminal = wt != null ? TerminalKind.WindowsTerminal : TerminalKind.Cmd;
        if (terminal == TerminalKind.WindowsTerminal && (wt == null || folder.Contains(';'))) terminal = TerminalKind.Cmd;

        var start = terminal switch
        {
            // wt treats ";" as a command separator, so keep it out of the arguments.
            TerminalKind.WindowsTerminal => new ProcessStartInfo(wt!)
            {
                ArgumentList =
                {
                    "new-tab", "-d", folder, "--title", "Claude – " + FolderName(folder).Replace(';', ','),
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
        start.UseShellExecute = true;

        try
        {
            Process.Start(start);
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException("Konsole konnte nicht gestartet werden: " + ex.Message, ex);
        }

        Remember(settings, folder);
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

    private static string? FindWindowsTerminal()
    {
        // wt.exe is an app execution alias; File.Exists sees it in WindowsApps.
        string alias = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WindowsApps", "wt.exe");
        return FindOnPath("wt.exe") ?? (File.Exists(alias) ? alias : null);
    }

    private static string? FindOnPath(string file)
    {
        foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
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
}
