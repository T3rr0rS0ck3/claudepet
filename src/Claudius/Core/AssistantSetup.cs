using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Claudius.Shared;

namespace Claudius.Core;

public enum SetupStatus { BridgeMissing, NotConfigured, Connected, OtherStatusLine }

/// <summary>Registers ClaudiusBridge.exe as statusLine command in the assistant's user settings.</summary>
public static class AssistantSetup
{
    /// <summary>
    /// The Store version's install folder changes with every update and is closed to other programs, so
    /// The assistant reaches its bridge through the package's app execution alias instead.
    /// </summary>
    public static string BridgePath => AppPackage.IsPackaged
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WindowsApps", BridgeName + ".exe")
        : Path.Combine(AppContext.BaseDirectory, BridgeName + ".exe");

    private const string BridgeName = "ClaudiusBridge";

    /// <summary>A statusLine or hook command running this app's bridge, or the one from before the rename.</summary>
    private static bool IsOurCommand(string command) =>
        command.Contains(BridgeName, StringComparison.OrdinalIgnoreCase)
        || command.Contains(Legacy.OldBridgeName, StringComparison.OrdinalIgnoreCase);

    public static string ConfigDir
    {
        get
        {
            string? configDir = Environment.GetEnvironmentVariable(AssistantCli.ConfigDirVariable);
            return string.IsNullOrWhiteSpace(configDir)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), AssistantCli.ConfigFolder)
                : configDir;
        }
    }

    public static string SettingsPath => Path.Combine(ConfigDir, "settings.json");

    /// <summary>Custom theme that colors the assistant's start-up mascot like the pet.</summary>
    public const string ThemeSlug = "claudius";
    public static string ThemesDir => Path.Combine(ConfigDir, "themes");
    public static string ThemePath => Path.Combine(ThemesDir, ThemeSlug + ".json");
    private const string ThemeValue = "custom:" + ThemeSlug;

    /// <summary>
    /// The assistant runs the command through Git Bash (or PowerShell without Git Bash), so the path
    /// needs forward slashes. Unquoted works in both shells; quoting is only needed for spaces.
    /// </summary>
    public static string BridgeCommand
    {
        get
        {
            string path = BridgePath.Replace('\\', '/');
            return path.Contains(' ') ? $"\"{path}\"" : path;
        }
    }

    public static string? CurrentCommand()
    {
        try
        {
            var root = Load();
            return root?["statusLine"]?["command"]?.GetValue<string>();
        }
        catch
        {
            return null;
        }
    }

    public static SetupStatus GetStatus()
    {
        string? command = CurrentCommand();
        if (command != null && IsOurCommand(command))
            return SetupStatus.Connected;
        if (!File.Exists(BridgePath)) return SetupStatus.BridgeMissing;
        return command == null ? SetupStatus.NotConfigured : SetupStatus.OtherStatusLine;
    }

    /// <summary>Registers the statusLine and, with <paramref name="hooks"/>, the ?/! session hooks.</summary>
    public static void Install(bool hooks = true)
    {
        var root = Load() ?? new JsonObject();
        Backup();
        root["statusLine"] = new JsonObject
        {
            ["type"] = "command",
            ["command"] = BridgeCommand,
            ["padding"] = 0,
        };
        RemoveHooks(root);
        if (hooks) AddHooks(root);
        Save(root);
    }

    /// <summary>
    /// Connected, but to another copy of the bridge (e.g. a portable copy before switching to the Store
    /// version): point the statusLine at this app's bridge. Returns true if it changed anything.
    /// </summary>
    public static bool Repoint()
    {
        if (GetStatus() != SetupStatus.Connected || CurrentCommand() == BridgeCommand || !File.Exists(BridgePath))
            return false;
        var root = Load();
        if (root?["statusLine"] is not JsonObject statusLine) return false;
        Backup();
        statusLine["command"] = BridgeCommand;
        Save(root);
        return true;
    }

    public static void Uninstall()
    {
        var root = Load();
        if (root == null) return;
        bool connected = GetStatus() == SetupStatus.Connected;
        if (!connected && !HasAnyHook()) return;
        Backup();
        if (connected) root.Remove("statusLine");
        RemoveHooks(root);
        Save(root);
    }

    // ---------------------------------------------------------------- session hooks (? and ! on the pet)

    /// <summary>
    /// Hook events the bridge listens to, with a tool matcher where needed. Kept to rare events:
    /// every hook call starts the bridge, and per-tool hooks would slow the assistant down.
    /// </summary>
    private static readonly (string Event, string? Matcher)[] HookEvents =
    [
        ("SessionStart", null), ("UserPromptSubmit", null), ("Stop", null), ("PermissionRequest", null),
        ("Notification", null), ("PreToolUse", "AskUserQuestion"), ("PostToolUse", "AskUserQuestion"),
        ("SessionEnd", null),
    ];

    public static string HookCommand => BridgeCommand + " --hook";

    /// <summary>All of the pet's hooks are registered, pointing at this app's bridge.</summary>
    public static bool HooksInstalled()
    {
        try
        {
            return Load()?["hooks"] is JsonObject hooks && HookEvents.All(h => hooks[h.Event] is JsonArray groups && groups.Any(IsCurrent));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Adds or removes only the pet's own hooks; other hooks are left alone. Hooks pointing at
    /// another copy of the bridge (e.g. after switching between installed and self-built app) are
    /// replaced. Never registers hooks for a missing bridge, they would fail in every session.
    /// </summary>
    public static void SetHooks(bool enabled)
    {
        if (enabled ? HooksInstalled() || !File.Exists(BridgePath) : !HasAnyHook()) return;
        var root = Load() ?? new JsonObject();
        Backup();
        RemoveHooks(root);
        if (enabled) AddHooks(root);
        Save(root);
    }

    private static void AddHooks(JsonObject root)
    {
        if (root["hooks"] is not JsonObject hooks) root["hooks"] = hooks = new JsonObject();
        foreach (var (name, matcher) in HookEvents)
        {
            if (hooks[name] is not JsonArray groups) hooks[name] = groups = new JsonArray();
            var group = new JsonObject();
            if (matcher != null) group["matcher"] = matcher;
            group["hooks"] = new JsonArray(new JsonObject
            {
                ["type"] = "command",
                ["command"] = HookCommand,
                ["timeout"] = 10,
            });
            groups.Add(group);
        }
    }

    private static void RemoveHooks(JsonObject root)
    {
        if (root["hooks"] is not JsonObject hooks) return;
        foreach (var (name, value) in hooks.ToList())
        {
            if (value is not JsonArray groups) continue;
            foreach (var group in groups.OfType<JsonObject>().ToList())
            {
                if (group["hooks"] is not JsonArray list) continue;
                foreach (var hook in list.Where(IsOurHook).ToList()) list.Remove(hook);
                if (list.Count == 0) groups.Remove(group);
            }
            if (groups.Count == 0) hooks.Remove(name);
        }
        if (hooks.Count == 0) root.Remove("hooks");
    }

    private static bool HasAnyHook()
    {
        try
        {
            return Load()?["hooks"] is JsonObject hooks
                   && hooks.Any(h => h.Value is JsonArray groups && groups.Any(g => g?["hooks"] is JsonArray list && list.Any(IsOurHook)));
        }
        catch
        {
            return false;
        }
    }

    private static bool IsCurrent(JsonNode? group) =>
        group?["hooks"] is JsonArray list && list.Any(h => IsOurHook(h)
            && h?["command"]?.GetValue<string>() == HookCommand);

    private static bool IsOurHook(JsonNode? hook) =>
        hook?["command"] is JsonValue command && command.TryGetValue(out string? text)
        && IsOurCommand(text);

    /// <summary>Whether the assistant's voice dictation (<c>voice.enabled</c>) is switched on.</summary>
    public static bool IsVoiceEnabled()
    {
        try
        {
            return Load()?["voice"]?["enabled"]?.GetValue<bool>() == true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Sets <c>voice.enabled</c>; other voice options (e.g. the mode) are kept.</summary>
    public static void SetVoiceEnabled(bool enabled)
    {
        if (IsVoiceEnabled() == enabled) return;
        var root = Load() ?? new JsonObject();
        Backup();
        if (root["voice"] is not JsonObject voice) root["voice"] = voice = new JsonObject();
        voice["enabled"] = enabled;
        Save(root);
    }

    /// <summary>The <c>theme</c> currently selected in the assistant's settings.json, or null if none is set.</summary>
    public static string? CurrentTheme()
    {
        try
        {
            return Load()?["theme"]?.GetValue<string>();
        }
        catch
        {
            return null;
        }
    }

    public static bool IsPetThemeSelected() => CurrentTheme() == ThemeValue;

    /// <summary>
    /// Writes the pet theme: the given base theme plus the mascot color. <c>clawd_body</c> is not in the
    /// documented token list; unknown tokens are ignored by the assistant, so at worst the mascot stays orange.
    /// The assistant watches the themes folder, so running sessions pick up a new color without a restart.
    /// </summary>
    public static void WritePetTheme(string color, string? baseTheme)
    {
        string themeBase = "dark";
        var overrides = new JsonObject();
        if (baseTheme is { } theme && theme.StartsWith("custom:") && theme != ThemeValue)
        {
            // Based on another custom theme: take over its base and colors.
            try
            {
                string file = Path.Combine(ThemesDir, theme["custom:".Length..] + ".json");
                if (File.Exists(file) && JsonNode.Parse(File.ReadAllText(file), documentOptions: ReadOptions) is JsonObject other)
                {
                    themeBase = other["base"]?.GetValue<string>() ?? themeBase;
                    if (other["overrides"] is JsonObject o) overrides = (JsonObject)o.DeepClone();
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
            {
                Log.Write("Theme " + theme + " konnte nicht gelesen werden: " + ex.Message);
            }
        }
        else if (baseTheme is "dark" or "light" or "dark-daltonized" or "light-daltonized" or "dark-ansi" or "light-ansi")
        {
            themeBase = baseTheme;
        }
        overrides["clawd_body"] = color;

        string json = new JsonObject
        {
            ["name"] = "Claudius",
            ["base"] = themeBase,
            ["overrides"] = overrides,
        }.ToJsonString(WriteOptions);
        // Only touch the file when something changed: every write makes running sessions reload the theme.
        if (File.Exists(ThemePath) && File.ReadAllText(ThemePath) == json) return;
        Directory.CreateDirectory(ThemesDir);
        File.WriteAllText(ThemePath, json, new UTF8Encoding(false));
    }

    /// <summary>Selects the pet theme in settings.json (or another theme / none again when switching back).</summary>
    public static void SelectTheme(string? theme)
    {
        if (CurrentTheme() == theme) return;
        var root = Load() ?? new JsonObject();
        Backup();
        if (theme == null) root.Remove("theme");
        else root["theme"] = theme;
        Save(root);
    }

    public static void SelectPetTheme() => SelectTheme(ThemeValue);

    public static void DeletePetTheme()
    {
        if (File.Exists(ThemePath)) File.Delete(ThemePath);
    }

    private static JsonObject? Load()
    {
        if (!File.Exists(SettingsPath)) return null;
        string text = File.ReadAllText(SettingsPath);
        if (string.IsNullOrWhiteSpace(text)) return null;
        return JsonNode.Parse(text, documentOptions: ReadOptions) as JsonObject ?? throw new InvalidDataException(Strings.NotJsonObject);
    }

    private static void Backup()
    {
        if (File.Exists(SettingsPath))
            File.Copy(SettingsPath, SettingsPath + ".claudius-backup", overwrite: true);
    }

    private static void Save(JsonObject root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, root.ToJsonString(WriteOptions), new UTF8Encoding(false));
    }

    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
