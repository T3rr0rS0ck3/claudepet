namespace Claudius.Shared;

/// <summary>
/// Names fixed by the AI assistant's command-line tool the pet works with: its command, its process, its
/// config folder and the environment variables it reads or sets. They are not the pet's to choose, so
/// they are kept here and nowhere else.
/// </summary>
public static class AssistantCli
{
    /// <summary>The command, and the process name of both the CLI and its desktop app.</summary>
    public const string Command = "claude";
    public const string Exe = Command + ".exe";
    public const string Cmd = Command + ".cmd";

    /// <summary>Config folder in the user profile, holding settings.json and themes.</summary>
    public const string ConfigFolder = "." + Command;
    /// <summary>Overrides <see cref="ConfigFolder"/>.</summary>
    public const string ConfigDirVariable = "CLAUDE_CONFIG_DIR";
    /// <summary>Set for child processes: "cli" in a terminal, something else in the desktop app.</summary>
    public const string EntrypointVariable = "CLAUDE_CODE_ENTRYPOINT";
}
