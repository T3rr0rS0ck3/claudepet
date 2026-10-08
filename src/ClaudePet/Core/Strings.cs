using ClaudePet.Pet;

namespace ClaudePet.Core;

/// <summary>
/// Every text of the user interface in English and German, side by side. XAML uses the properties via
/// <c>{x:Static core:Strings.Name}</c>, so a window picks up the language when it is opened; menus that
/// live on are rebuilt when it changes. The speech bubble texts are in <see cref="AppSettings.DefaultTexts"/>.
/// </summary>
public static class Strings
{
    public const string English = "en";
    public const string German = "de";

    private static bool _german;

    /// <summary>The current language, <see cref="English"/> or <see cref="German"/>.</summary>
    public static string Current => _german ? German : English;

    /// <summary>Switches the language (<see cref="English"/> or <see cref="German"/>).</summary>
    public static void Use(string language) => _german = language == German;

    /// <summary>Culture for dates and weekdays in the current language.</summary>
    public static System.Globalization.CultureInfo Culture =>
        System.Globalization.CultureInfo.GetCultureInfo(_german ? "de-DE" : "en-GB");

    private static string L(string en, string de) => _german ? de : en;

    // ---------------------------------------------------------------- general

    public static string Close => L("Close", "Schließen");
    public static string Percent(string number) => _german ? number + " %" : number + "%";

    // ---------------------------------------------------------------- settings window

    public static string SettingsTitle => L("Claudius – Settings", "Claudius – Einstellungen");
    public static string SettingsHeading => L("SETTINGS", "EINSTELLUNGEN");
    public static string Save => L("Save", "Speichern");
    public static string Cancel => L("Cancel", "Abbrechen");

    public static string GroupGeneral => L("GENERAL", "ALLGEMEIN");
    public static string Language => L("Language", "Sprache");

    public static string ConnectButton => L("Connect Claude Code", "Claude Code verbinden");
    public static string Disconnect => L("Disconnect", "Trennen");
    public static string SessionMarks => L("“?” on questions and a speech bubble when Claude is done",
        "„?“ bei Fragen und Sprechblase wenn Claude fertig ist");
    public static string SessionMarksTip => L(
        "Adds hooks to Claude Code's settings (only when connected). Switching it off removes them again.",
        "Trägt Hooks in die Claude-Code-Einstellungen ein (nur wenn verbunden). Ausschalten entfernt sie wieder.");
    public static string SessionPets => L("A baby pet for each Claude session", "Baby-Pet für jede Claude-Session");
    public static string SessionPetsTip => L("A small pet per running session follows the pet and shows its “?”.",
        "Ein kleines Pet pro laufender Session folgt dem Pet und zeigt deren „?“.");
    public static string SetupConnected => L("✔ Connected – ClaudePetBridge is set as statusLine.",
        "✔ Verbunden – ClaudePetBridge ist als statusLine eingetragen.");
    public static string SetupOtherStatusLine => L("Another statusLine is already set:\n",
        "Es ist bereits eine andere statusLine eingetragen:\n");
    public static string SetupBridgeMissing => L("ClaudePetBridge.exe was not found (it must be next to ClaudePet.exe).",
        "ClaudePetBridge.exe wurde nicht gefunden (muss neben ClaudePet.exe liegen).");
    public static string SetupNotConnected => L("Not connected.", "Nicht verbunden.");

    public static string GroupLaunch => L("LAUNCH CLAUDE", "CLAUDE STARTEN");
    public static string ReposFolder => L("Repo folder", "Repo-Ordner");
    public static string ReposFolderTip => L("Folder containing all your projects", "Ordner, in dem alle Projekte liegen");
    public static string OpenClaudeIn => L("Open Claude in", "Claude öffnen in");
    public static string TerminalAuto(bool windowsTerminal) => windowsTerminal
        ? L("Automatic (Windows Terminal)", "Automatisch (Windows Terminal)")
        : L("Automatic (cmd)", "Automatisch (cmd)");
    public static string TerminalCmd => L("Command Prompt (cmd)", "Eingabeaufforderung (cmd)");
    public static string TerminalDesktop => L("Claude Desktop (Code tab)", "Claude Desktop (Code-Tab)");
    public static string GhostDrag => L("Dragging the pet onto Explorer with the left mouse button opens Claude there",
        "Pet mit linker Maustaste auf Explorer ziehen öffnet Claude dort");
    public static string VoiceOffer => L("Offer voice chat (menu item “Start voice chat…”)",
        "Sprachchat anbieten (Menüeintrag „Sprachchat starten…“)");
    public static string VoiceOfferTip => L(
        "Uses Claude Code's voice dictation. Needs a claude.ai account and a microphone. Switching it off also turns dictation in Claude Code off again.",
        "Nutzt das Sprachdiktat von Claude Code. Benötigt ein claude.ai-Konto und ein Mikrofon. Ausschalten schaltet auch das Diktat in Claude Code wieder ab.");
    public static string ClaudeFound(string path) => L("Claude Code found: ", "Claude Code gefunden: ") + path;
    public static string ClaudeNotFound => L("Claude Code was not found (claude is not in the PATH).",
        "Claude Code wurde nicht gefunden (claude ist nicht im PATH).");

    public static string Size => L("Size", "Größe");
    public static string Color => L("Color", "Farbe");
    public static string PickColor => L("Pick…", "Wählen…");
    public static string DefaultColor => L("Default", "Standard");
    public static string MascotColor => L("Claude mascot in the terminal in the pet's color",
        "Claude-Maskottchen im Terminal in Pet-Farbe");
    public static string MascotColorTip => L(
        "Creates a Claude Code theme of its own (themes\\claudepet.json) and selects it. Switching it off restores the previous theme.",
        "Legt ein eigenes Claude-Code-Theme an (themes\\claudepet.json) und wählt es aus. Beim Ausschalten wird das vorherige Theme wiederhergestellt.");
    public static string AlwaysOnTop => L("Always on top", "Immer im Vordergrund");
    public static string Animations => L("Animations", "Animationen");
    public static string WalkAroundDesktop => L("Walk around the desktop", "Auf dem Desktop herumlaufen");
    public static string CrossMonitors => L("Move between monitors", "Zwischen Monitoren wechseln");
    public static string CrossMonitorsTip => L(
        "Walks or hops over to the neighbouring monitor where the monitors touch in the Windows arrangement.",
        "Läuft oder hüpft dort zum Nachbarmonitor, wo sich die Monitore laut Windows-Anordnung berühren.");
    public static string Emotes => L("Emotes on hover (feed, pat, …)", "Emotes beim Drüberfahren (Füttern, Streicheln, …)");
    public static string ModelOutfits => L("Outfit for the Claude model", "Outfit passend zum Claude-Modell");
    public static string ModelOutfitsTip => L(
        "Opus wears a crown, Sonnet sunglasses, Haiku a flower, Fable a wizard's hat. Baby pets dress for their own session's model.",
        "Opus trägt eine Krone, Sonnet eine Sonnenbrille, Haiku eine Blume, Fable einen Zauberhut. Baby-Pets ziehen sich für das Modell ihrer Session an.");
    public static string StartWithWindows => L("Start with Windows", "Mit Windows starten");
    public static string AutostartBlocked => L("Switched off in Windows –", "In Windows ausgeschaltet –");
    public static string OpenStartupSettings => L("Open startup settings", "Autostart-Einstellungen öffnen");

    public static string IntervalField => L("Refresh interval", "Aktualisierungsintervall");
    public static string SessionWarnField => L("Session warning thresholds", "Session-Warnschwellen");
    public static string WeekWarnField => L("Weekly warning thresholds", "Wochen-Warnschwellen");
    public static string MoodField => L("Mood thresholds", "Zustandsgrenzen");
    public static string IntervalLabel => IntervalField + " (s)";
    public static string SessionWarnLabel => SessionWarnField + " (%)";
    public static string WeekWarnLabel => WeekWarnField + " (%)";
    public static string MoodLabelText => MoodField + " (%)";
    public static string MoodOrder => L("normal, attentive, nervous, worried, panic, asleep",
        "normal, aufmerksam, nervös, besorgt, Panik, schlafen");
    public static string MoodOrderHint => L("Order: ", "Reihenfolge: ") + MoodOrder;
    public static string ShowForecast => L("Show forecast", "Prognose anzeigen");

    public static string GroupBubbles => L("SPEECH BUBBLES & NOTIFICATIONS", "SPRECHBLASEN & BENACHRICHTIGUNGEN");
    public static string ShowBubbles => L("Show speech bubbles", "Sprechblasen anzeigen");
    public static string BubbleDuration => L("Display time (s)", "Anzeigedauer (s)");
    public static string YourName => L("Your name", "Dein Name");
    public static string WindowsNotifications => L("Windows notifications", "Windows-Benachrichtigungen");
    public static string EditTexts => L("Edit texts…", "Texte bearbeiten…");
    public static string EditTextsTip => L("Opens settings.json; changes are picked up automatically.",
        "Öffnet settings.json; Änderungen werden automatisch übernommen.");
    public static string TestNotification => L("Test notification", "Test-Benachrichtigung");
    public static string TestNotificationText => L("This is what warnings look like. 🟠", "So sehen Warnungen aus. 🟠");

    public static string AutoUpdates => L("Check for updates automatically", "Automatisch nach Updates suchen");
    public static string AutoUpdatesTip => L("Asks GitHub for a new version at startup and once a day.",
        "Fragt beim Start und einmal am Tag bei GitHub nach einer neuen Version.");
    public static string CheckNow => L("Check now", "Jetzt suchen");
    public static string InstalledVersion(string version) => L("Installed version: ", "Installierte Version: ") + version;
    public static string DevBuildSuffix => L(" (self-built, no updates)", " (selbst gebaut, keine Updates)");
    public static string PortableSuffix => L(" (portable)", " (portabel)");
    public static string StoreVersion(string version) => L(
        $"Version {version} – updates come through the Microsoft Store.",
        $"Version {version} – Updates kommen über den Microsoft Store.");
    public static string Checking => L("Checking…", "Suche…");
    public static string UpToDate => L("✔ You have the latest version.", "✔ Du hast die neueste Version.");
    public static string CheckFailed => L("Check failed: ", "Suche fehlgeschlagen: ");
    public static string Install => L("Install", "Installieren");
    public static string Download => L("Download", "Herunterladen");
    public static string VersionAvailable(string version) => L($"Version {version} is available.", $"Version {version} ist verfügbar.");

    public static string InvalidNumber(string field) => L($"{field}: invalid number.", $"{field}: ungültige Zahl.");
    public static string MoodCountError => L("Mood thresholds: give exactly 6 values.", "Zustandsgrenzen: genau 6 Werte angeben.");
    public static string MoodOrderError => L("Mood thresholds must be ascending.", "Zustandsgrenzen müssen aufsteigend sein.");
    public static string ColorError => L("Color: please give it as #RRGGBB, e.g. #D97757.",
        "Farbe: bitte als #RRGGBB angeben, z. B. #D97757.");
    public static string ReposFolderMissing => L("The repo folder does not exist.", "Repo-Ordner existiert nicht.");
    public static string ClaudeSettingsNotWritten => L("Claude Code's settings could not be written: ",
        "Claude-Code-Einstellungen konnten nicht geschrieben werden: ");

    // ---------------------------------------------------------------- usage overlay

    public static string SessionHeading => L("SESSION (5 HOURS)", "SESSION (5 STUNDEN)");
    public static string WeekHeading => L("WEEK (7 DAYS)", "WOCHE (7 TAGE)");
    public static string ResetWaiting => L("Reset – waiting for new data", "Zurückgesetzt – warte auf neue Daten");
    public static string SessionResetIn(string duration, string clock) =>
        L($"Reset in {duration} · {clock}", $"Reset in {duration} · {clock} Uhr");
    public static string WeekResetAt(string dayTime) => $"Reset: {dayTime}";
    public static string LimitIn(string duration) => $"Limit in {duration}";
    public static string LimitAt(string dayTime) => $"Limit {dayTime}";
    public static string NoData => L("No data", "Keine Daten");
    public static string Forecast(string text) => L("Forecast: ", "Prognose: ") + text;
    public static string ForecastLasts => L("Forecast: lasts until the reset", "Prognose: reicht bis zum Reset");
    public static string UpdatedJustNow => L("Updated just now", "Aktualisiert gerade eben");
    public static string UpdatedAgo(string duration) => L($"Updated {duration} ago", $"Aktualisiert vor {duration}");
    public static string HintNotConnected => L(
        "Claude Code isn't connected yet. Right-click the pet → “Connect Claude Code”.",
        "Claude Code ist noch nicht verbunden. Rechtsklick auf das Pet → „Claude Code verbinden“.");
    public static string HintBridgeMissing => L("ClaudePetBridge.exe is missing next to ClaudePet.exe.",
        "ClaudePetBridge.exe fehlt neben ClaudePet.exe.");
    public static string HintFirstReply => L(
        "Connected. The values appear after the first reply in Claude Code (Pro/Max only).",
        "Verbunden. Die Werte erscheinen nach der ersten Antwort in Claude Code (nur Pro/Max).");
    public static string HintStale => L("The data is older than 5 hours – open Claude Code to refresh it.",
        "Daten sind älter als 5 Stunden – öffne Claude Code, um sie zu aktualisieren.");

    /// <summary>The pet's own state, shown in the usage overlay.</summary>
    public static string MoodLabel(PetMood mood) => mood switch
    {
        PetMood.Relaxed => L("Claudius is relaxed", "Claudius ist entspannt"),
        PetMood.Normal => L("Claudius is in a good mood", "Claudius ist gut drauf"),
        PetMood.Attentive => L("Claudius is paying attention", "Claudius ist aufmerksam"),
        PetMood.Nervous => L("Claudius is getting nervous", "Claudius wird nervös"),
        PetMood.Worried => L("Claudius is worried", "Claudius ist besorgt"),
        PetMood.Panic => L("Claudius is panicking!", "Claudius ist in Panik!"),
        PetMood.Exhausted => L("Claudius sleeps until the reset", "Claudius schläft bis zum Reset"),
        _ => L("Claudius is waiting for data", "Claudius wartet auf Daten"),
    };

    // ---------------------------------------------------------------- menus and tray

    public static string ShowUsage => L("Show usage", "Usage anzeigen");
    public static string OpenClaude => L("Open Claude…", "Claude öffnen…");
    public static string StartVoiceChat => L("Start voice chat…", "Sprachchat starten…");
    public static string SayHello => L("Say hello", "Hallo sagen");
    public static string ShowPet => L("Show pet", "Pet anzeigen");
    public static string WalkAround => L("Walk around", "Herumlaufen");
    public static string MinimizeToTray => L("Minimize to tray", "In den Tray minimieren");
    public static string ConnectMenu => L("Connect Claude Code…", "Claude Code verbinden…");
    public static string SettingsMenu => L("Settings…", "Einstellungen…");
    public static string Quit => L("Quit", "Beenden");
    public static string InstallUpdateMenu(string version) => L($"Install update to {version}…", $"Update auf {version} installieren…");
    public static string DownloadVersionMenu(string version) => L($"Download version {version}…", $"Version {version} herunterladen…");
    public static string TrayWaiting => L("Claudius – waiting for data", "Claudius – warte auf Daten");
    public static string TrayUsage(string session, string week) =>
        L($"Claudius – session {session} · week {week}", $"Claudius – Session {session} · Woche {week}");

    public static string RecentlyOpened => L("Recently opened", "Zuletzt geöffnet");
    public static string ChooseOtherFolder => L("Choose another folder…", "Anderen Ordner wählen…");
    public static string SetReposFolder => L("Set repo folder…", "Repo-Ordner festlegen…");
    public static string ChooseReposFolder => L("Choose the folder with your projects", "Ordner mit deinen Projekten wählen");
    public static string ChooseLaunchFolder => L("Which folder should Claude start in?", "In welchem Ordner soll Claude starten?");

    public static string EmoteName(Emote emote) => emote switch
    {
        Emote.Feed => L("Feed", "Füttern"),
        Emote.Pat => L("Pat", "Streicheln"),
        Emote.Play => L("Play", "Spielen"),
        _ => L("Tickle", "Kitzeln"),
    };

    /// <summary>Tooltip of a baby pet: what Claude is doing in that session.</summary>
    public static string SessionTip(string folder, string state) => state switch
    {
        Shared.SessionStates.Question => L($"{folder}: Claude is waiting for you", $"{folder}: Claude wartet auf dich"),
        Shared.SessionStates.Done => L($"{folder}: Claude is done", $"{folder}: Claude ist fertig"),
        Shared.SessionStates.Working => L($"{folder}: Claude is working", $"{folder}: Claude arbeitet"),
        _ => folder,
    };

    // ---------------------------------------------------------------- notifications and dialogs

    public static string LimitUsedUp(bool session, string reset) => session
        ? L($"Your session limit is used up. Reset {reset}.", $"Dein Session-Limit ist aufgebraucht. Reset {reset}.")
        : L($"Your weekly limit is used up. Reset {reset}.", $"Dein Wochenlimit ist aufgebraucht. Reset {reset}.");
    public static string LimitReached(bool session, string percent) => session
        ? L($"Your session limit is at {percent}.", $"Dein Session-Limit ist bei {percent}.")
        : L($"Your weekly limit is at {percent}.", $"Dein Wochenlimit ist bei {percent}.");
    public static string ThemeNotWritten => L("The Claude Code theme could not be written: ",
        "Claude-Code-Theme konnte nicht geschrieben werden: ");
    public static string VoiceEnableQuestion(string path) => L(
        $"Voice chat switches on Claude Code's voice dictation (voice.enabled in {path}, a backup is made).\n\n" +
        "Requirements: signed in with a claude.ai account and microphone access for the console " +
        "(Windows Settings → Privacy → Microphone).\n\nSwitch it on?",
        $"Für den Sprachchat wird das Sprachdiktat von Claude Code eingeschaltet (voice.enabled in {path}, eine Sicherung wird angelegt).\n\n" +
        "Voraussetzungen: Anmeldung mit einem claude.ai-Konto und Mikrofonzugriff für die Konsole " +
        "(Windows-Einstellungen → Datenschutz → Mikrofon).\n\nEinschalten?");
    public static string ClaudeSettingsError => L("Error writing Claude Code's settings:\n",
        "Fehler beim Schreiben der Claude-Code-Einstellungen:\n");
    public static string DesktopFailed => L(
        "Claude Desktop could not be opened. Is the desktop app installed and Claude Code up to date (claude update)?",
        "Claude Desktop konnte nicht geöffnet werden. Ist die Desktop-App installiert und Claude Code aktuell (claude update)?");
    public static string DesktopOpenFailed => L("Claude Desktop could not be opened: ", "Claude Desktop konnte nicht geöffnet werden: ");
    public static string ConsoleStartFailed => L("The console could not be started: ", "Konsole konnte nicht gestartet werden: ");
    public static string AlreadyConnected => L("Claude Code is already connected to Claudius.",
        "Claude Code ist bereits mit Claudius verbunden.");
    public static string BridgeNotFound(string path) => L("ClaudePetBridge.exe was not found:\n", "ClaudePetBridge.exe wurde nicht gefunden:\n") + path;
    public static string ReplaceStatusLine(string path, string? command) => L(
        $"A statusLine is already set in {path}:\n\n{command}\n\nReplace it with Claudius? (A backup is made.)",
        $"In {path} ist bereits eine statusLine eingetragen:\n\n{command}\n\nDurch Claudius ersetzen? (Eine Sicherung wird angelegt.)");
    public static string AddStatusLine(string path) => L(
        $"Claudius adds itself as statusLine to\n{path}\nContinue?",
        $"Claudius trägt sich als statusLine in\n{path}\nein. Fortfahren?");
    public static string Connected => L(
        "Connected! The usage values appear after the next reply in Claude Code (running sessions pick up the change automatically).",
        "Verbunden! Die Usage-Werte erscheinen nach der nächsten Antwort in Claude Code (laufende Sessions übernehmen die Änderung automatisch).");
    public static string UpdateNotification(string version) => L(
        $"Version {version} is available (right-click the pet).", $"Version {version} ist verfügbar (Rechtsklick aufs Pet).");
    public static string InstallUpdateQuestion(string version) => L(
        $"Install version {version}?\n\nClaudius closes briefly for this and restarts automatically afterwards.",
        $"Version {version} installieren?\n\nClaudius wird dafür kurz geschlossen und danach automatisch neu gestartet.");
    public static string UpdateFailed => L("The update could not be installed:\n", "Das Update konnte nicht installiert werden:\n");
    public static string NoChecksums => L("The release contains no SHA256SUMS.txt.", "Das Release enthält keine SHA256SUMS.txt.");
    public static string NoChecksumFor(string file) => L($"No checksum found for {file}.", $"Keine Prüfsumme für {file} gefunden.");
    public static string ChecksumMismatch => L("The download's checksum does not match – update cancelled.",
        "Die Prüfsumme des Downloads stimmt nicht – Update abgebrochen.");
    public static string NotJsonObject => L("settings.json is not a JSON object.", "settings.json ist kein JSON-Objekt.");
}
