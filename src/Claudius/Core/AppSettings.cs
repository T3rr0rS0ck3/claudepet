using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Claudius.Pet;
using Claudius.Shared;

namespace Claudius.Core;

public sealed class MoodThresholds
{
    public int Normal { get; set; } = 50;
    public int Attentive { get; set; } = 70;
    public int Nervous { get; set; } = 85;
    public int Worried { get; set; } = 90;
    public int Panic { get; set; } = 95;
    public int Exhausted { get; set; } = 100;
}

/// <summary>Where the assistant is opened; <see cref="Desktop"/> is the Code tab of the desktop app.</summary>
public enum TerminalKind { Auto, WindowsTerminal, Cmd, PowerShell, Desktop }

public sealed class AppSettings
{
    public const string DefaultPetColor = "#D97757";

    /// <summary>
    /// UI language, <see cref="Strings.English"/> or <see cref="Strings.German"/>. New installs start in
    /// English; settings saved before there was a choice were German and stay so.
    /// </summary>
    public string? Language { get; set; }

    // Pet
    public double PetScale { get; set; } = 5;
    public double? PositionX { get; set; }
    public double? PositionY { get; set; }
    public bool AlwaysOnTop { get; set; } = true;
    public bool Animations { get; set; } = true;
    /// <summary>Walk around on the taskbar and on top of open windows.</summary>
    public bool WalkAround { get; set; } = true;
    /// <summary>While walking around, also walk and hop over to neighbouring monitors.</summary>
    public bool CrossMonitors { get; set; }
    /// <summary>Buttons to feed, pat, play with and tickle the pet while hovering it.</summary>
    public bool Emotes { get; set; } = true;
    /// <summary>Dress for the AI model in use: crown for Opus, sunglasses for Sonnet, a flower for Haiku.</summary>
    public bool ModelOutfits { get; set; } = true;
    /// <summary>Look for new releases on GitHub at start and once a day.</summary>
    public bool CheckForUpdates { get; set; } = true;
    /// <summary>The last version the pet announced, so each update is mentioned only once.</summary>
    public string? NotifiedUpdate { get; set; }
    /// <summary>"?" when an assistant session asks something, a bubble when it is done (via the assistant's hooks).</summary>
    public bool SessionMarks { get; set; } = true;
    /// <summary>A small pet per running the assistant session, showing that session's "?".</summary>
    public bool SessionPets { get; set; }
    public bool StartWithWindows { get; set; }
    /// <summary>Body color as "#RRGGBB"; shade and mood tints are derived from it.</summary>
    public string PetColor { get; set; } = DefaultPetColor;
    /// <summary>Colors the assistant's start-up mascot like the pet (via a custom theme).</summary>
    public bool TerminalMascotColor { get; set; }
    /// <summary>Whether Claudius switched the assistant to its theme, and which theme was selected before.</summary>
    public bool MascotThemeSwitched { get; set; }
    public string? MascotThemeBefore { get; set; }

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
    /// Bubble texts per event in the UI language; one variant is picked at random.
    /// Placeholders: {name}, {NAME}, {percent}, {session}, {week}, {folder}, {version}.
    /// </summary>
    public Dictionary<string, List<string>> Texts { get; set; } = [];
    /// <summary>Which rewordings of the default texts have been applied, see <see cref="RewordedTexts"/>.</summary>
    public int TextsVersion { get; set; }

    // Notifications
    public bool Notifications { get; set; } = true;

    // Opening the assistant
    /// <summary>Folder containing all projects; its subfolders are offered on double-click.</summary>
    public string? ReposPath { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TerminalKind Terminal { get; set; } = TerminalKind.Auto;
    /// <summary>Most recently opened project folders, newest first.</summary>
    public List<string> RecentProjects { get; set; } = [];
    /// <summary>Dragging the pet with the left mouse button onto an Explorer window opens the assistant there.</summary>
    public bool GhostDrag { get; set; } = true;
    /// <summary>Offers "Start voice chat…" (the assistant's voice dictation) in the menus.</summary>
    public bool VoiceChat { get; set; }

    /// <summary>Switches the UI language; the bubble texts start over with that language's defaults.</summary>
    public void SetLanguage(string language)
    {
        if (language == Language) return;
        Language = language;
        Texts = DefaultTexts(language);
    }

    public static Dictionary<string, List<string>> DefaultTexts(string? language) =>
        language == Strings.German ? GermanTexts() : EnglishTexts();

    private static Dictionary<string, List<string>> EnglishTexts() => new()
    {
        ["Greeting"] = ["Hi {name}! Session {session}%, week {week}%."],
        ["NoData"] = ["Waiting for data from your AI assistant…\n(Right-click → Connect AI assistant)"],
        ["Normal"] = ["All relaxed so far."],
        ["Attentive"] = ["We're getting busy..."],
        ["Nervous"] = ["Uhm... we should start being careful."],
        ["Worried"] = ["{percent}%! 😰"],
        ["Panic"] = ["{NAME}. ALMOST EMPTY."],
        ["Exhausted"] = ["Okay... I'll sleep until the reset."],
        ["Reset"] = ["Fresh quota! Let's go!", "Well rested. Let's carry on!"],
        ["Poke"] = ["Hey!", "I'm keeping watch, promise.", "Session {session}%, week {week}%."],
        ["Launch"] = ["Have fun in {folder}!", "Off we go: {folder}"],
        ["NoFolder"] = ["There's no folder here I can open…"],
        ["Voice"] = ["Hold the space bar and talk to your AI assistant 🎤"],
        // Relayed from the AI assistant: it is the one asking or done, the pet only passes it on.
        ["Question"] = ["{folder}: The assistant has a question.", "Psst, the assistant is waiting for you in {folder}."],
        ["Done"] = ["{folder}: The assistant is done!", "The assistant is done in {folder}."],
        ["NoWindow"] = ["I can't find the window of {folder}…"],
        ["Feed"] = ["Mmm, yummy! 🍪", "Cookies are the best token food.", "*munch munch*"],
        ["Pat"] = ["Aww, that's nice ❤", "More of that!", "You're the best, {name}."],
        ["Play"] = ["Catch! ⚽", "Again, again!", "I'm a pro juggler."],
        ["Update"] = ["Version {version} is out! Right-click → Install update.", "Psst, {name}: there's a new version ({version})."],
        ["Updating"] = ["Downloading the update… see you soon!"],
        ["Tickle"] = ["Hehehe! Stop it! 😆", "Not there, I'm ticklish!", "Hahaha… mercy!"],
    };

    private static Dictionary<string, List<string>> GermanTexts() => new()
    {
        ["Greeting"] = ["Hi {name}! Session {session} %, Woche {week} %."],
        ["NoData"] = ["Ich warte auf Daten vom KI-Assistenten…\n(Rechtsklick → KI-Assistent verbinden)"],
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
        ["Voice"] = ["Halte die Leertaste gedrückt und sprich mit dem KI-Assistenten 🎤"],
        // Relayed from the AI assistant: it is the one asking or done, the pet only passes it on.
        ["Question"] = ["{folder}: Der Assistent hat eine Frage.", "Psst, der Assistent wartet in {folder} auf dich."],
        ["Done"] = ["{folder}: Der Assistent ist fertig!", "Der Assistent ist fertig in {folder}."],
        ["NoWindow"] = ["Ich finde das Fenster von {folder} nicht…"],
        ["Feed"] = ["Mmmh, lecker! 🍪", "Kekse sind das beste Token-Futter.", "*mampf mampf*"],
        ["Pat"] = ["Hach, das ist schön ❤", "Mehr davon!", "Du bist der Beste, {name}."],
        ["Play"] = ["Fang! ⚽", "Nochmal, nochmal!", "Ich bin ein Profi-Jongleur."],
        ["Update"] = ["Version {version} ist da! Rechtsklick → Update installieren.", "Psst, {name}: Es gibt eine neue Version ({version})."],
        ["Updating"] = ["Lade das Update… bis gleich!"],
        ["Tickle"] = ["Hihihi! Aufhören! 😆", "Nicht da, da bin ich kitzlig!", "Hahaha… Gnade!"],
    };

    public static AppSettings Load()
    {
        AppSettings settings;
        try
        {
            if (File.Exists(DataPaths.SettingsFile))
            {
                var json = JsonNode.Parse(File.ReadAllText(DataPaths.SettingsFile), documentOptions: ReadOptions) as JsonObject;
                if (json != null) Legacy.RenameSettings(json);
                settings = json?.Deserialize<AppSettings>(JsonOptions) ?? new();
                settings.Language ??= Strings.German; // saved before the language could be chosen
            }
            else
            {
                settings = new();
            }
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
        Language = Language == Strings.German ? Strings.German : Strings.English;
        var defaults = DefaultTexts(Language);
        foreach (var (version, keys) in RewordedTexts)
        {
            if (TextsVersion >= version) continue;
            foreach (var key in keys) Texts[key] = defaults[key];
        }
        TextsVersion = RewordedTexts[^1].Version;
        foreach (var (key, value) in defaults)
        {
            if (!Texts.TryGetValue(key, out var list) || list == null || list.Count == 0)
                Texts[key] = value;
        }
    }

    /// <summary>
    /// Texts whose defaults were reworded, by <see cref="TextsVersion"/>: saved settings from before get the
    /// new wording once, replacing their own. Add an entry with the next version when changing a default text.
    /// </summary>
    private static readonly (int Version, string[] Keys)[] RewordedTexts =
    [
        (1, ["Question", "Done"]), // the assistant asks and finishes, not the pet
        (2, ["NoData", "Voice", "Question", "Done"]), // no third-party brand names in the texts
    ];

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];

    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
