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

    public static string SettingsPath
    {
        get
        {
            string? configDir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
            if (string.IsNullOrWhiteSpace(configDir))
                configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
            return Path.Combine(configDir, "settings.json");
        }
    }

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

    private static JsonObject? Load()
    {
        if (!File.Exists(SettingsPath)) return null;
        string text = File.ReadAllText(SettingsPath);
        if (string.IsNullOrWhiteSpace(text)) return null;
        return JsonNode.Parse(text, documentOptions: new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        }) as JsonObject ?? throw new InvalidDataException("settings.json ist kein JSON-Objekt.");
    }

    private static void Backup()
    {
        if (File.Exists(SettingsPath))
            File.Copy(SettingsPath, SettingsPath + ".claudepet-backup", overwrite: true);
    }

    private static void Save(JsonObject root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }), new UTF8Encoding(false));
    }
}
