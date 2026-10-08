using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClaudePet.Core;

public enum SetupStatus { BridgeMissing, NotConfigured, Connected, OtherStatusLine }

/// <summary>Registers ClaudePetBridge.exe as statusLine command in the Claude Code user settings.</summary>
public static class ClaudeCodeSetup
{
    public static string BridgePath => Path.Combine(AppContext.BaseDirectory, "ClaudePetBridge.exe");

    public static string ConfigDir
    {
        get
        {
            string? configDir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
            return string.IsNullOrWhiteSpace(configDir)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude")
                : configDir;
        }
    }

    public static string SettingsPath => Path.Combine(ConfigDir, "settings.json");

    /// <summary>Custom theme that colors Claude Code's start-up mascot like the pet.</summary>
    public const string ThemeSlug = "claudepet";
    public static string ThemesDir => Path.Combine(ConfigDir, "themes");
    public static string ThemePath => Path.Combine(ThemesDir, ThemeSlug + ".json");
    private const string ThemeValue = "custom:" + ThemeSlug;

    /// <summary>
    /// Claude Code runs the command through Git Bash (or PowerShell without Git Bash), so the path
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
        if (command != null && command.Contains("ClaudePetBridge", StringComparison.OrdinalIgnoreCase))
            return SetupStatus.Connected;
        if (!File.Exists(BridgePath)) return SetupStatus.BridgeMissing;
        return command == null ? SetupStatus.NotConfigured : SetupStatus.OtherStatusLine;
    }

    public static void Install()
    {
        var root = Load() ?? new JsonObject();
        Backup();
        root["statusLine"] = new JsonObject
        {
            ["type"] = "command",
            ["command"] = BridgeCommand,
            ["padding"] = 0,
        };
        Save(root);
    }

    public static void Uninstall()
    {
        var root = Load();
        if (root == null || GetStatus() != SetupStatus.Connected) return;
        Backup();
        root.Remove("statusLine");
        Save(root);
    }

    /// <summary>Whether Claude Code's voice dictation (<c>voice.enabled</c>) is switched on.</summary>
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

    /// <summary>The <c>theme</c> currently selected in Claude Code's settings.json, or null if none is set.</summary>
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
    /// documented token list; unknown tokens are ignored by Claude Code, so at worst the mascot stays orange.
    /// Claude Code watches the themes folder, so running sessions pick up a new color without a restart.
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
            ["name"] = "Claude Pet",
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
        return JsonNode.Parse(text, documentOptions: ReadOptions) as JsonObject ?? throw new InvalidDataException("settings.json ist kein JSON-Objekt.");
    }

    private static void Backup()
    {
        if (File.Exists(SettingsPath))
            File.Copy(SettingsPath, SettingsPath + ".claudepet-backup", overwrite: true);
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
