using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Claudius.Core;
using Claudius.Pet;
using Claudius.Shared;
using Claudius.Views;

namespace Claudius;

public partial class App : Application
{
    private const string MutexName = "Claudius.SingleInstance.v1";
    private const string ShowEventName = "Claudius.Show.v1";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private readonly UsageMonitor _monitor = new();
    private readonly VoiceWatcher _voice = new();
    private readonly SessionMonitor _sessions = new();
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Random _random = new();
    private readonly WarnState _sessionWarn = new();
    private readonly WarnState _weekWarn = new();
    private readonly DispatcherTimer _updateTimer = new();
    private bool _updating;

    private PetWindow _pet = null!;
    private TrayIcon? _tray;
    private UsageOverlay? _overlay;
    private DateTime _overlayClosedAt;
    private SettingsWindow? _settingsWindow;
    private UsageState _state = UsageState.Compute(null, new MoodThresholds(), DateTimeOffset.Now);
    private PetMood? _lastMood;
    private bool? _wasNight;
    private DateTime _settingsWrite;

    public AppSettings Settings { get; private set; } = null!;
    public bool PetVisible => _pet.IsVisible;
    /// <summary>A newer release found by the last update check.</summary>
    public UpdateInfo? AvailableUpdate { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (HandleInstallerCommand(e.Args))
        {
            Shutdown();
            return;
        }

        _mutex = new Mutex(true, MutexName, out bool firstInstance);
        if (!firstInstance)
        {
            // Already running: ask the running instance to show itself.
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { }
            Shutdown();
            return;
        }
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        ThreadPool.RegisterWaitForSingleObject(_showEvent,
            (_, _) => Dispatcher.BeginInvoke(() => { ShowPet(); ShowUsage(); }), null, -1, false);

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Write(args.Exception.ToString());
            args.Handled = true;
        };

        Legacy.MigrateDataDir();
        Settings = AppSettings.Load();
        Strings.Use(Settings.Language!);
        Settings.Save();
        _settingsWrite = File.GetLastWriteTimeUtc(DataPaths.SettingsFile);
        Legacy.RemoveOldAutostart();
        if (Settings.StartWithWindows != Autostart.IsEnabled()) Autostart.Set(Settings.StartWithWindows);
        Legacy.MigrateTheme(Settings);

        ApplyPetColor();
        _pet = new PetWindow();
        _pet.ApplySettings(Settings, initial: true);
        _pet.Clicked += OnPetClicked;
        _pet.DoubleClicked += ShowProjectMenu;
        _pet.SessionClicked += GoToSession;
        _pet.GhostDropped += OnGhostDropped;
        _pet.Moved += SavePosition;
        _pet.Emoted += emote => Say(emote.ToString());
        _pet.PetImage.ContextMenu = BuildContextMenu();
        _pet.Show();

        _tray = new TrayIcon(this);

        // Load the current state silently first: no bubbles/notifications for values that were
        // already reached before the app started.
        _monitor.Start(Settings.PollIntervalSeconds);
        Evaluate(initial: true);
        _monitor.Changed += () => Evaluate(initial: false);
        _clock.Tick += (_, _) => OnClock();
        _clock.Start();
        _voice.ListeningChanged += listening => _pet.SetListening(listening);
        _voice.Start();

        // "?" and bubbles from the assistant's hooks; the first read is silent like the usage values.
        SyncSessionHooks();
        _sessions.Changed += ApplySessions;
        _sessions.Poll();
        _sessions.Attention += session =>
        {
            if (Settings.SessionMarks) Say(session.State == SessionStates.Question ? "Question" : "Done", session.Label);
        };

        Say(_state.Mood == PetMood.Unknown ? "NoData" : "Greeting");

        _updateTimer.Tick += (_, _) =>
        {
            _updateTimer.Interval = TimeSpan.FromHours(24);
            _ = CheckForUpdateAsync(silent: true);
        };
        SyncUpdateChecks();
    }

    /// <summary>Silent commands used by the installer. Returns true if one was handled.</summary>
    private static bool HandleInstallerCommand(string[] args)
    {
        try
        {
            if (args.Contains("--connect-assistant"))
            {
                // Never replace someone's existing status line without asking.
                if (AssistantSetup.GetStatus() == SetupStatus.NotConfigured) AssistantSetup.Install();
                return true;
            }
            if (args.Contains("--uninstall-cleanup"))
            {
                AssistantSetup.Uninstall();
                Autostart.Set(false);
                Legacy.RemoveOldAutostart();
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Write("Installer-Befehl fehlgeschlagen: " + ex);
            return true;
        }
        return false;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // The pet may have wandered off since the last drag.
        if (_pet != null && Settings?.WalkAround == true)
        {
            try
            {
                Settings.PositionX = _pet.Left;
                Settings.PositionY = _pet.Top;
                Settings.Save();
            }
            catch (IOException) { }
        }
        _tray?.Dispose();
        _showEvent?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    // ---------------------------------------------------------------- usage evaluation

    private void OnClock()
    {
        // Pick up hand edits of settings.json (e.g. bubble texts).
        try
        {
            var write = File.GetLastWriteTimeUtc(DataPaths.SettingsFile);
            if (write != _settingsWrite)
            {
                _settingsWrite = write;
                Settings = AppSettings.Load();
                ApplySettings(save: false);
            }
        }
        catch (IOException) { }

        // Re-evaluate every second: reset times pass, "working" expires, countdowns tick.
        Evaluate(initial: false);
        _sessions.Poll();
    }

    private void Evaluate(bool initial)
    {
        var now = DateTimeOffset.Now;
        _state = UsageState.Compute(_monitor.Current, Settings.Thresholds, now);
        var mood = _state.Mood;

        if (!initial && _lastMood is { } last && mood != last && mood != PetMood.Unknown)
        {
            if (mood > last && mood >= PetMood.Normal)
            {
                Say(mood.ToString());
            }
            else if (mood < last && last >= PetMood.Nervous && mood <= PetMood.Normal)
            {
                Say("Reset");
                _pet.Cheer(TimeSpan.FromSeconds(4));
            }
        }
        _lastMood = mood;
        _pet.SetMood(mood, Working);
        _pet.SetOutfit(Settings.ModelOutfits ? Sprite.OutfitFor(_state.Snapshot?.Model) : Outfit.None);
        ApplyOccasion(initial);

        var snapshot = _state.Snapshot;
        CheckWarning(true, snapshot?.FiveHour, _state.Session, Settings.SessionWarnThresholds, _sessionWarn, initial, now);
        CheckWarning(false, snapshot?.SevenDay, _state.Week, Settings.WeekWarnThresholds, _weekWarn, initial, now);

        _tray?.Update(mood, _state.Max == null
            ? Strings.TrayWaiting
            : Strings.TrayUsage(Strings.Percent(PercentText(_state.Session)), Strings.Percent(PercentText(_state.Week))));

        _overlay?.Update(_state, _monitor.History, Settings, AssistantSetup.GetStatus(), now);
    }

    /// <summary>Seasonal or night headwear and sleepiness; says good night when the night starts, not on start-up.</summary>
    private void ApplyOccasion(bool initial)
    {
        var time = Occasions.Now;
        bool night = Occasions.IsNight(time, Settings);
        _pet.SetAccessory(Occasions.For(time, Settings));
        _pet.SetNight(night);
        if (night && _wasNight == false && !initial) Say("GoodNight");
        if (!night && _wasNight == true && !initial) Say("GoodMorning");
        _wasNight = night;
    }

    private sealed class WarnState
    {
        public long ResetsAt;
        public int Highest;
    }

    private void CheckWarning(bool session, RateWindow? window, double? value, List<int> thresholds,
        WarnState state, bool initial, DateTimeOffset now)
    {
        if (window == null || value == null) return;
        if (Math.Abs(window.ResetsAt - state.ResetsAt) > 600)
        {
            state.ResetsAt = window.ResetsAt;   // new window: re-arm all thresholds
            state.Highest = 0;
        }

        int crossed = thresholds.Where(t => value >= t).DefaultIfEmpty(0).Max();
        if (crossed <= state.Highest) return;
        state.Highest = crossed;
        if (initial || !Settings.Notifications) return;

        string text = value >= 100
            ? Strings.LimitUsedUp(session, Format.DayTime(window.ResetsAtTime, now))
            : Strings.LimitReached(session, Strings.Percent(Format.Percent(value.Value)));
        ShowNotification("Claudius", text);
    }

    private void Say(string key, string? folder = null)
    {
        if (!Settings.SpeechBubbles) return;
        if (!Settings.Texts.TryGetValue(key, out var variants) || variants.Count == 0) return;

        string name = Settings.UserName;
        string text = variants[_random.Next(variants.Count)]
            .Replace("{NAME}", name.ToUpperInvariant())
            .Replace("{name}", name)
            .Replace("{folder}", folder ?? "")
            .Replace("{percent}", PercentText(_state.Max))
            .Replace("{session}", PercentText(_state.Session))
            .Replace("{week}", PercentText(_state.Week))
            .Replace("{version}", AvailableUpdate?.VersionText ?? "")
            .Replace(" %", " %"); // keep "62 %" together when wrapping
        _pet.Say(text, Settings.BubbleDurationSeconds);
    }

    private static string PercentText(double? value) => value is { } v ? Format.Percent(v) : "–";

    // ---------------------------------------------------------------- actions (menus, tray, settings)

    public void ShowNotification(string title, string text) => _tray?.ShowBalloon(title, text);

    public void ShowUsage()
    {
        if (_overlay != null)
        {
            _overlay.Activate();
            return;
        }

        _overlay = new UsageOverlay();
        _overlay.Closed += (_, _) =>
        {
            _overlay = null;
            _overlayClosedAt = DateTime.Now;
            _pet.PauseWalking(false);
        };
        _pet.PauseWalking(true);
        _overlay.Update(_state, _monitor.History, Settings, AssistantSetup.GetStatus(), DateTimeOffset.Now);

        Rect anchor;
        if (_pet.IsVisible)
        {
            anchor = _pet.PetBounds;
        }
        else
        {
            var area = SystemParameters.WorkArea;
            anchor = new Rect(area.Right - 180, area.Bottom, 160, 0);
        }
        _overlay.ShowNear(anchor, _pet);
    }

    private void OnPetClicked(DateTime releasedAt)
    {
        // The click that deactivated (and closed) the overlay should not reopen it.
        if (_overlay == null && releasedAt - _overlayClosedAt < TimeSpan.FromMilliseconds(600)) return;
        if (_overlay != null) _overlay.Close();
        else ShowUsage();
    }

    /// <summary>Brings the terminal tab or Desktop app window of a session to the front.</summary>
    private async void GoToSession(SessionView session)
    {
        if (await SessionWindow.ActivateAsync(session)) return;
        Log.Write("Kein Fenster für Session " + session.Id + " in " + session.Folder + " gefunden.");
        Say("NoWindow", session.Label);
    }

    public void ShowPet()
    {
        if (!_pet.IsVisible) _pet.Show();
    }

    public void TogglePetVisible()
    {
        if (_pet.IsVisible) _pet.Hide();
        else _pet.Show();
    }

    public void ToggleAlwaysOnTop()
    {
        Settings.AlwaysOnTop = !Settings.AlwaysOnTop;
        ApplySettings(save: true);
    }

    public void ToggleWalkAround()
    {
        Settings.WalkAround = !Settings.WalkAround;
        // Stay where it is when it stops walking.
        Settings.PositionX = _pet.Left;
        Settings.PositionY = _pet.Top;
        ApplySettings(save: true);
    }

    public void ShowSettings()
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }
        _settingsWindow = new SettingsWindow(this, Settings);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void ApplySessions()
    {
        _pet.SetSessions(_sessions.Sessions, _sessions.Overall, Settings.SessionMarks, Settings.SessionPets,
            Settings.ModelOutfits);
        _pet.SetMood(_state.Mood, Working);
    }

    /// <summary>
    /// The assistant is working: the status line just reported, or a hooked session is busy. The latter also
    /// covers the Desktop app, which may not run the status line.
    /// </summary>
    private bool Working => _state.Active || (Settings.SessionMarks && _sessions.AnyWorking);

    /// <summary>
    /// Once the assistant is connected: statusLine and ?/! hooks point at this copy's bridge, and the hooks
    /// follow the setting.
    /// </summary>
    private void SyncSessionHooks()
    {
        try
        {
            if (AssistantSetup.GetStatus() != SetupStatus.Connected) return;
            if (AssistantSetup.Repoint()) Log.Write("statusLine auf " + AssistantSetup.BridgeCommand + " umgestellt.");
            AssistantSetup.SetHooks(Settings.SessionMarks);
        }
        catch (Exception ex)
        {
            Log.Write("Hooks des KI-Assistenten konnten nicht angepasst werden: " + ex.Message);
        }
    }

    public void ApplySettings(bool save)
    {
        if (save) SaveSettings();
        if (Settings.Language != Strings.Current) ApplyLanguage();
        SyncSessionHooks();
        ApplySessions();
        ApplyPetColor();
        SyncMascotTheme();
        _pet.ApplySettings(Settings);
        _monitor.SetInterval(Settings.PollIntervalSeconds);
        if (Settings.StartWithWindows != Autostart.IsEnabled()) Autostart.Set(Settings.StartWithWindows);
        SyncUpdateChecks();
        Evaluate(initial: true);
    }

    /// <summary>Menus that stay around are built again in the new language; windows pick it up when opened.</summary>
    private void ApplyLanguage()
    {
        Strings.Use(Settings.Language!);
        _pet.PetImage.ContextMenu = BuildContextMenu();
        _pet.ApplyLanguage();
    }

    /// <summary>Keeps the assistant's theme in line with <see cref="AppSettings.TerminalMascotColor"/>.</summary>
    private void SyncMascotTheme()
    {
        try
        {
            if (Settings.TerminalMascotColor)
            {
                if (!Settings.MascotThemeSwitched)
                {
                    string? current = AssistantSetup.CurrentTheme();
                    Settings.MascotThemeBefore = current == "custom:" + AssistantSetup.ThemeSlug ? null : current;
                }
                AssistantSetup.WritePetTheme(Settings.PetColor, Settings.MascotThemeBefore);
                if (!Settings.MascotThemeSwitched)
                {
                    // Only switch once: if the user picks another theme in the assistant later, that choice stays.
                    AssistantSetup.SelectPetTheme();
                    Settings.MascotThemeSwitched = true;
                    SaveSettings();
                }
            }
            else if (Settings.MascotThemeSwitched)
            {
                if (AssistantSetup.IsPetThemeSelected()) AssistantSetup.SelectTheme(Settings.MascotThemeBefore);
                AssistantSetup.DeletePetTheme();
                Settings.MascotThemeSwitched = false;
                Settings.MascotThemeBefore = null;
                SaveSettings();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
                                       or System.Text.Json.JsonException)
        {
            Log.Write("Theme des KI-Assistenten konnte nicht geschrieben werden: " + ex);
            ShowNotification("Claudius", Strings.ThemeNotWritten + ex.Message);
        }
    }

    /// <summary>The pet's color is also the accent color of the overlay, menus and speech bubble.</summary>
    private void ApplyPetColor() => ApplyPetColor(ParsePetColor());

    private void ApplyPetColor(uint color)
    {
        Sprite.SetBodyColor(color);
        var accent = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(
            (byte)(color >> 16), (byte)(color >> 8), (byte)color));
        accent.Freeze();
        Resources["AccentBrush"] = accent;
    }

    /// <summary>
    /// Shows a color on the pet, tray and accents without saving it, while it is being picked in the settings.
    /// Null goes back to the saved color.
    /// </summary>
    public void PreviewPetColor(uint? color)
    {
        int previous = Sprite.Version;
        ApplyPetColor(color ?? ParsePetColor());
        if (Sprite.Version == previous) return;
        _pet.Redraw();
        Evaluate(initial: true);
    }

    private uint ParsePetColor() =>
        Sprite.TryParseColor(Settings.PetColor, out uint color) ? color : Sprite.DefaultBodyColor;

    private void SaveSettings()
    {
        try
        {
            Settings.Save();
            _settingsWrite = File.GetLastWriteTimeUtc(DataPaths.SettingsFile);
        }
        catch (IOException ex)
        {
            Log.Write("Einstellungen konnten nicht gespeichert werden: " + ex.Message);
        }
    }

    // ---------------------------------------------------------------- opening the assistant

    public void ShowProjectMenu() => ShowProjectMenu(voice: false);

    private void ShowProjectMenu(bool voice)
    {
        // First use: nothing to offer yet, so ask for the repo folder right away.
        if (Settings.ReposPath == null && Settings.RecentProjects.Count == 0 && !ChooseReposFolder()) return;

        var menu = ProjectMenu.Build(Settings, folder => LaunchAssistant(folder, voice), () => ChooseAndLaunch(voice), () =>
        {
            if (ChooseReposFolder()) ShowProjectMenu(voice);
        });
        if (_pet.IsVisible)
        {
            menu.PlacementTarget = _pet.PetImage;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
        }
        menu.IsOpen = true;
    }

    /// <summary>
    /// The assistant has no flag to start in voice mode, so voice dictation is switched on in its
    /// settings and the user is told how to talk (hold Space).
    /// </summary>
    public void StartVoiceChat()
    {
        if (!Settings.VoiceChat) return;
        if (!AssistantSetup.IsVoiceEnabled())
        {
            var answer = MessageBox.Show(Strings.VoiceEnableQuestion(AssistantSetup.SettingsPath),
                "Claudius", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;
            try
            {
                AssistantSetup.SetVoiceEnabled(true);
            }
            catch (Exception ex)
            {
                Log.Write("Sprachdiktat konnte nicht eingeschaltet werden: " + ex);
                MessageBox.Show(Strings.AssistantSettingsError + ex.Message, "Claudius",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }
        ShowProjectMenu(voice: true);
    }

    public void LaunchAssistant(string folder) => LaunchAssistant(folder, voice: false);

    private void LaunchAssistant(string folder, bool voice)
    {
        try
        {
            if (AssistantLauncher.Launch(Settings, folder) is { } desktop) _ = ReportDesktopFailure(desktop);
        }
        catch (DirectoryNotFoundException)
        {
            Settings.RecentProjects.Remove(folder);
            SaveSettings();
            Say("NoFolder");
            return;
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException)
        {
            Log.Write("KI-Assistent konnte nicht gestartet werden: " + ex.Message);
            MessageBox.Show(ex.Message, "Claudius", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        SaveSettings();
        _pet.Cheer(TimeSpan.FromSeconds(2));
        Say(voice ? "Voice" : "Launch", AssistantLauncher.FolderName(folder));
    }

    /// <summary>The CLI's <c>--desktop</c> fails quietly (CLI too old, Desktop missing); say so instead.</summary>
    private async Task ReportDesktopFailure(System.Diagnostics.Process process)
    {
        using (process)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { return; }
            if (process.ExitCode == 0) return;
        }
        Log.Write(Strings.DesktopFailed);
        MessageBox.Show(Strings.DesktopFailed, "Claudius", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void OnGhostDropped(string? folder, bool overShell)
    {
        if (folder != null) LaunchAssistant(folder);
        else if (overShell) Say("NoFolder");   // e.g. "This PC"; anywhere else the drop is just a cancel
    }

    private void ChooseAndLaunch(bool voice)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = Strings.ChooseLaunchFolder };
        if (Settings.ReposPath != null) dialog.InitialDirectory = Settings.ReposPath;
        if (dialog.ShowDialog() == true) LaunchAssistant(dialog.FolderName, voice);
    }

    /// <summary>Asks for the folder containing all projects. Returns false if cancelled.</summary>
    private bool ChooseReposFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = Strings.ChooseReposFolder };
        if (Settings.ReposPath != null) dialog.InitialDirectory = Settings.ReposPath;
        if (dialog.ShowDialog() != true) return false;
        Settings.ReposPath = dialog.FolderName;
        SaveSettings();
        return true;
    }

    private void SavePosition()
    {
        Settings.PositionX = _pet.Left;
        Settings.PositionY = _pet.Top;
        ApplySettings(save: true);
    }

    public void ConnectAssistant(Window? owner)
    {
        const string title = "Claudius";
        var status = AssistantSetup.GetStatus();
        string path = AssistantSetup.SettingsPath;

        switch (status)
        {
            case SetupStatus.Connected:
                MessageBox.Show(Strings.AlreadyConnected, title,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            case SetupStatus.BridgeMissing:
                MessageBox.Show(Strings.BridgeNotFound(AssistantSetup.BridgePath), title,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
        }

        string question = status == SetupStatus.OtherStatusLine
            ? Strings.ReplaceStatusLine(path, AssistantSetup.CurrentCommand())
            : Strings.AddStatusLine(path);
        var answer = owner != null
            ? MessageBox.Show(owner, question, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
            : MessageBox.Show(question, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            AssistantSetup.Install(hooks: Settings.SessionMarks);
            MessageBox.Show(Strings.Connected, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log.Write("KI-Assistent konnte nicht verbunden werden: " + ex);
            MessageBox.Show(Strings.AssistantSettingsError + ex.Message, title,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        Evaluate(initial: false);
    }

    // ---------------------------------------------------------------- updates

    /// <summary>Looks for a new release about 30 s after the start and then once a day, unless switched off.</summary>
    private void SyncUpdateChecks()
    {
        // The Store version updates through the Store.
        bool on = Settings.CheckForUpdates && !Updater.IsDevBuild && !AppPackage.IsPackaged;
        if (on && !_updateTimer.IsEnabled)
        {
            _updateTimer.Interval = TimeSpan.FromSeconds(30);
            _updateTimer.Start();
        }
        else if (!on)
        {
            _updateTimer.Stop();
        }
    }

    /// <summary>
    /// Asks GitHub for a newer release and remembers it for the menus; the pet mentions each new version once.
    /// Network errors are only logged when silent, otherwise thrown.
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdateAsync(bool silent)
    {
        try
        {
            AvailableUpdate = await Updater.CheckAsync();
        }
        catch (Exception ex) when (silent)
        {
            Log.Write("Update-Prüfung fehlgeschlagen: " + ex.Message);
            return AvailableUpdate;
        }
        if (AvailableUpdate is { } update && Settings.NotifiedUpdate != update.VersionText)
        {
            Settings.NotifiedUpdate = update.VersionText;
            SaveSettings();
            if (Settings.SpeechBubbles) Say("Update");
            else ShowNotification("Claudius", Strings.UpdateNotification(update.VersionText));
        }
        return AvailableUpdate;
    }

    /// <summary>
    /// Installed via setup: asks, then downloads and installs the update; the setup starts the new version.
    /// Portable copies just open the release page.
    /// </summary>
    public async void InstallUpdate(Window? owner = null)
    {
        if (AvailableUpdate is not { } update || _updating) return;
        if (!Updater.IsInstalled)
        {
            OpenUrl(update.PageUrl);
            return;
        }

        const string title = "Claudius – Update";
        string question = Strings.InstallUpdateQuestion(update.VersionText);
        var answer = owner != null
            ? MessageBox.Show(owner, question, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
            : MessageBox.Show(question, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        _updating = true;
        Say("Updating");
        try
        {
            await Updater.InstallAsync(update);
            Quit();
        }
        catch (Exception ex)
        {
            Log.Write("Update fehlgeschlagen: " + ex);
            MessageBox.Show(Strings.UpdateFailed + ex.Message, title,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _updating = false;
        }
    }

    /// <summary>Menu text for the available update, depending on whether it can be installed in place.</summary>
    public string UpdateMenuText => AvailableUpdate is not { } update ? ""
        : Updater.IsInstalled ? Strings.InstallUpdateMenu(update.VersionText) : Strings.DownloadVersionMenu(update.VersionText);

    public static void OpenUrl(string url) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });

    public void Quit()
    {
        _overlay?.Close();
        _tray?.Dispose();
        _tray = null;
        Shutdown();
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu { Style = (Style)FindResource("PetMenu") };
        MenuItem Item(string header, Action action)
        {
            var item = new MenuItem { Header = header };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
            return item;
        }

        Item(Strings.ShowUsage, ShowUsage);
        Item(Strings.OpenAssistant, ShowProjectMenu);
        var voice = Item(Strings.StartVoiceChat, StartVoiceChat);
        Item(Strings.SayHello, () =>
        {
            _pet.Cheer(TimeSpan.FromSeconds(2));
            if (Settings.SpeechBubbles) Say("Poke");
        });
        menu.Items.Add(new Separator());
        var onTop = Item(Strings.AlwaysOnTop, ToggleAlwaysOnTop);
        onTop.IsCheckable = true;
        var walk = Item(Strings.WalkAround, ToggleWalkAround);
        walk.IsCheckable = true;
        Item(Strings.MinimizeToTray, () => _pet.Hide());
        menu.Items.Add(new Separator());
        var update = Item("", () => InstallUpdate());
        Item(Strings.ConnectMenu, () => ConnectAssistant(null));
        Item(Strings.SettingsMenu, ShowSettings);
        menu.Items.Add(new Separator());
        Item(Strings.Quit, Quit);

        menu.Opened += (_, _) =>
        {
            onTop.IsChecked = Settings.AlwaysOnTop;
            walk.IsChecked = Settings.WalkAround;
            voice.Visibility = Settings.VoiceChat ? Visibility.Visible : Visibility.Collapsed;
            update.Header = UpdateMenuText;
            update.Visibility = AvailableUpdate != null ? Visibility.Visible : Visibility.Collapsed;
        };
        return menu;
    }
}
