using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudePet.Pet;
using ClaudePet.Shared;

namespace ClaudePet.Core;

public sealed class MoodThresholds
{
    public int Normal { get; set; } = 50;
    public int Attentive { get; set; } = 70;
    public int Nervous { get; set; } = 85;
    public int Worried { get; set; } = 90;
    public int Panic { get; set; } = 95;
    public int Exhausted { get; set; } = 100;
}

public enum TerminalKind { Auto, WindowsTerminal, Cmd, PowerShell }

public sealed class AppSettings
{
    public const string DefaultPetColor = "#D97757";

    // Pet
    public double PetScale { get; set; } = 5;
    public double? PositionX { get; set; }
    public double? PositionY { get; set; }
    public bool AlwaysOnTop { get; set; } = true;
    public bool Animations { get; set; } = true;
    /// <summary>Walk around on the taskbar and on top of open windows.</summary>
    public bool WalkAround { get; set; } = true;
    public bool StartWithWindows { get; set; }
    /// <summary>Body color as "#RRGGBB"; shade and mood tints are derived from it.</summary>
    public string PetColor { get; set; } = DefaultPetColor;
    /// <summary>Colors Claude Code's start-up mascot like the pet (via a custom Claude Code theme).</summary>
    public bool ClaudeMascotColor { get; set; }
    /// <summary>Whether ClaudePet switched Claude Code to its theme, and which theme was selected before.</summary>
    public bool ClaudeThemeSwitched { get; set; }
    public string? ClaudeThemeBefore { get; set; }

    // Usage
    public int PollIntervalSeconds { get; set; } = 2;
    public bool ShowForecast { get; set; } = true;
    public MoodThresholds Thresholds { get; set; } = new();
    public List<int> SessionWarnThresholds { get; set; } = [90, 95, 100];
    public List<int> WeekWarnThresholds { get; set; } = [75, 90, 100];

    // Speech bubbles
    public bool SpeechBubbles { get; set; } = true;
    public double BubbleDurationSeconds { get; set; } = 6;
    /// <summary>Used for the {name} / {NAME} placeholders.</summary>
    public string UserName { get; set; } = Capitalize(Environment.UserName);
    /// <summary>
    /// Bubble texts per event; one variant is picked at random.
    /// Placeholders: {name}, {NAME}, {percent}, {session}, {week}.
    /// </summary>
    public Dictionary<string, List<string>> Texts { get; set; } = DefaultTexts();

    // Notifications
    public bool Notifications { get; set; } = true;

    // Opening Claude Code
    /// <summary>Folder containing all projects; its subfolders are offered on double-click.</summary>
    public string? ReposPath { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TerminalKind Terminal { get; set; } = TerminalKind.Auto;
    /// <summary>Most recently opened project folders, newest first.</summary>
    public List<string> RecentProjects { get; set; } = [];
    /// <summary>Dragging the pet with the right mouse button onto an Explorer window opens Claude there.</summary>
    public bool GhostDrag { get; set; } = true;
    /// <summary>Offers "Sprachchat starten…" (Claude Code voice dictation) in the menus.</summary>
    public bool VoiceChat { get; set; }

    public static Dictionary<string, List<string>> DefaultTexts() => new()
    {
        ["Greeting"] = ["Hi {name}! Session {session} %, Woche {week} %."],
        ["NoData"] = ["Ich warte auf Daten von Claude Code…\n(Rechtsklick → Claude Code verbinden)"],
        ["Normal"] = ["Alles noch entspannt."],
        ["Attentive"] = ["Wir werden langsam fleißig..."],
        ["Nervous"] = ["Ähm... wir sollten langsam aufpassen."],
        ["Worried"] = ["{percent} %! 😰"],
        ["Panic"] = ["{NAME}. FAST LEER."],
        ["Exhausted"] = ["Okay... ich schlafe jetzt bis zum Reset."],
        ["Reset"] = ["Frisches Kontingent! Los geht's!", "Ausgeschlafen. Weiter geht's!"],
        ["Poke"] = ["Hey!", "Ich pass auf, versprochen.", "Session {session} %, Woche {week} %."],
        ["Launch"] = ["Viel Spaß in {folder}!", "Auf geht's: {folder}"],
        ["NoFolder"] = ["Da ist kein Ordner, den ich öffnen kann…"],
        ["Voice"] = ["Halte die Leertaste gedrückt und sprich mit Claude 🎤"],
    };

    public static AppSettings Load()
    {
        AppSettings settings;
        try
        {
            settings = File.Exists(DataPaths.SettingsFile)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(DataPaths.SettingsFile), JsonOptions) ?? new()
                : new();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            Log.Write("Einstellungen konnten nicht gelesen werden, verwende Standardwerte: " + ex.Message);
            try { File.Copy(DataPaths.SettingsFile, DataPaths.SettingsFile + ".broken", true); } catch { }
            settings = new();
        }
        settings.Normalize();
        return settings;
    }

    public void Save()
    {
        Directory.CreateDirectory(DataPaths.DataDir);
        File.WriteAllText(DataPaths.SettingsFile, JsonSerializer.Serialize(this, JsonOptions), new UTF8Encoding(false));
    }

    private void Normalize()
    {
        PetScale = Math.Clamp(PetScale, 2, 12);
        PollIntervalSeconds = Math.Clamp(PollIntervalSeconds, 1, 300);
        BubbleDurationSeconds = Math.Clamp(BubbleDurationSeconds, 1, 60);
        Thresholds ??= new();
        SessionWarnThresholds ??= [];
        WeekWarnThresholds ??= [];
        UserName ??= "";
        Texts ??= [];
        RecentProjects ??= [];
        if (string.IsNullOrWhiteSpace(ReposPath)) ReposPath = null;
        PetColor = Sprite.TryParseColor(PetColor, out uint color) ? Sprite.ToHex(color) : DefaultPetColor;
        foreach (var (key, value) in DefaultTexts())
        {
            if (!Texts.TryGetValue(key, out var list) || list == null || list.Count == 0)
                Texts[key] = value;
        }
    }

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
