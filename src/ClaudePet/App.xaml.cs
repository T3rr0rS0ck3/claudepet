using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ClaudePet.Core;
using ClaudePet.Pet;
using ClaudePet.Shared;
using ClaudePet.Views;

namespace ClaudePet;

public partial class App : Application
{
    private const string MutexName = "ClaudePet.SingleInstance.v1";
    private const string ShowEventName = "ClaudePet.Show.v1";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private readonly UsageMonitor _monitor = new();
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Random _random = new();
    private readonly WarnState _sessionWarn = new();
    private readonly WarnState _weekWarn = new();

    private PetWindow _pet = null!;
    private TrayIcon? _tray;
    private UsageOverlay? _overlay;
    private DateTime _overlayClosedAt;
    private SettingsWindow? _settingsWindow;
    private UsageState _state = UsageState.Compute(null, new MoodThresholds(), DateTimeOffset.Now);
    private PetMood? _lastMood;
    private DateTime _settingsWrite;

    public AppSettings Settings { get; private set; } = null!;
    public bool PetVisible => _pet.IsVisible;

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

        Settings = AppSettings.Load();
        Settings.Save();
        _settingsWrite = File.GetLastWriteTimeUtc(DataPaths.SettingsFile);
        if (Settings.StartWithWindows != Autostart.IsEnabled()) Autostart.Set(Settings.StartWithWindows);

        _pet = new PetWindow();
        _pet.ApplySettings(Settings, initial: true);
        _pet.Clicked += OnPetClicked;
        _pet.DoubleClicked += ShowProjectMenu;
        _pet.Moved += SavePosition;
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

        Say(_state.Mood == PetMood.Unknown ? "NoData" : "Greeting");
    }

    /// <summary>Silent commands used by the installer. Returns true if one was handled.</summary>
    private static bool HandleInstallerCommand(string[] args)
    {
        try
        {
            if (args.Contains("--connect-claude-code"))
            {
                // Never replace someone's existing status line without asking.
                if (ClaudeCodeSetup.GetStatus() == SetupStatus.NotConfigured) ClaudeCodeSetup.Install();
                return true;
            }
            if (args.Contains("--uninstall-cleanup"))
            {
                ClaudeCodeSetup.Uninstall();
                Autostart.Set(false);
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
        _pet.SetMood(mood, _state.Active);

        var snapshot = _state.Snapshot;
        CheckWarning("Session-Limit", snapshot?.FiveHour, _state.Session, Settings.SessionWarnThresholds, _sessionWarn, initial, now);
        CheckWarning("Wochenlimit", snapshot?.SevenDay, _state.Week, Settings.WeekWarnThresholds, _weekWarn, initial, now);

        _tray?.Update(mood, _state.Max == null
            ? "Claude Pet – warte auf Daten"
            : $"Claude Pet – Session {PercentText(_state.Session)} % · Woche {PercentText(_state.Week)} %");

        _overlay?.Update(_state, _monitor.History, Settings, ClaudeCodeSetup.GetStatus(), now);
    }

    private sealed class WarnState
    {
        public long ResetsAt;
        public int Highest;
    }

    private void CheckWarning(string label, RateWindow? window, double? value, List<int> thresholds,
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
            ? $"Dein {label} ist aufgebraucht. Reset {Format.DayTime(window.ResetsAtTime, now)}."
            : $"Dein {label} ist bei {Format.Percent(value.Value)} %.";
        ShowNotification("Claude Usage Pet", text);
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
        _overlay.Update(_state, _monitor.History, Settings, ClaudeCodeSetup.GetStatus(), DateTimeOffset.Now);

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

    public void ApplySettings(bool save)
    {
        if (save) SaveSettings();
        _pet.ApplySettings(Settings);
        _monitor.SetInterval(Settings.PollIntervalSeconds);
        if (Settings.StartWithWindows != Autostart.IsEnabled()) Autostart.Set(Settings.StartWithWindows);
        Evaluate(initial: true);
    }

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

    // ---------------------------------------------------------------- opening Claude Code

    public void ShowProjectMenu()
    {
        // First use: nothing to offer yet, so ask for the repo folder right away.
        if (Settings.ReposPath == null && Settings.RecentProjects.Count == 0 && !ChooseReposFolder()) return;

        var menu = ProjectMenu.Build(Settings, LaunchClaude, ChooseAndLaunch, () =>
        {
            if (ChooseReposFolder()) ShowProjectMenu();
        });
        if (_pet.IsVisible)
        {
            menu.PlacementTarget = _pet.PetImage;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
        }
        menu.IsOpen = true;
    }

    public void LaunchClaude(string folder)
    {
        try
        {
            ClaudeLauncher.Launch(Settings, folder);
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
            Log.Write("Claude konnte nicht gestartet werden: " + ex.Message);
            MessageBox.Show(ex.Message, "Claude Pet", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        SaveSettings();
        _pet.Cheer(TimeSpan.FromSeconds(2));
        Say("Launch", ClaudeLauncher.FolderName(folder));
    }

    private void ChooseAndLaunch()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "In welchem Ordner soll Claude starten?" };
        if (Settings.ReposPath != null) dialog.InitialDirectory = Settings.ReposPath;
        if (dialog.ShowDialog() == true) LaunchClaude(dialog.FolderName);
    }

    /// <summary>Asks for the folder containing all projects. Returns false if cancelled.</summary>
    private bool ChooseReposFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Ordner mit deinen Projekten wählen" };
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

    public void ConnectClaudeCode(Window? owner)
    {
        const string title = "Claude Pet";
        var status = ClaudeCodeSetup.GetStatus();
        string path = ClaudeCodeSetup.SettingsPath;

        switch (status)
        {
            case SetupStatus.Connected:
                MessageBox.Show("Claude Code ist bereits mit Claude Pet verbunden.", title,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            case SetupStatus.BridgeMissing:
                MessageBox.Show($"ClaudePetBridge.exe wurde nicht gefunden:\n{ClaudeCodeSetup.BridgePath}", title,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
        }

        string question = status == SetupStatus.OtherStatusLine
            ? $"In {path} ist bereits eine statusLine eingetragen:\n\n{ClaudeCodeSetup.CurrentCommand()}\n\n" +
              "Durch Claude Pet ersetzen? (Eine Sicherung wird angelegt.)"
            : $"Claude Pet trägt sich als statusLine in\n{path}\nein. Fortfahren?";
        var answer = owner != null
            ? MessageBox.Show(owner, question, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
            : MessageBox.Show(question, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            ClaudeCodeSetup.Install();
            MessageBox.Show("Verbunden! Die Usage-Werte erscheinen nach der nächsten Antwort in Claude Code " +
                            "(laufende Sessions übernehmen die Änderung automatisch).", title,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log.Write("Claude Code konnte nicht verbunden werden: " + ex);
            MessageBox.Show("Fehler beim Schreiben der Claude-Code-Einstellungen:\n" + ex.Message, title,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        Evaluate(initial: false);
    }

    public void Quit()
    {
        _overlay?.Close();
        _tray?.Dispose();
        _tray = null;
        Shutdown();
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();
        MenuItem Item(string header, Action action)
        {
            var item = new MenuItem { Header = header };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
            return item;
        }

        Item("Usage anzeigen", ShowUsage);
        Item("Claude öffnen…", ShowProjectMenu);
        Item("Hallo sagen", () =>
        {
            _pet.Cheer(TimeSpan.FromSeconds(2));
            if (Settings.SpeechBubbles) Say("Poke");
        });
        menu.Items.Add(new Separator());
        var onTop = Item("Immer im Vordergrund", ToggleAlwaysOnTop);
        onTop.IsCheckable = true;
        var walk = Item("Herumlaufen", ToggleWalkAround);
        walk.IsCheckable = true;
        Item("In den Tray minimieren", () => _pet.Hide());
        menu.Items.Add(new Separator());
        Item("Claude Code verbinden…", () => ConnectClaudeCode(null));
        Item("Einstellungen…", ShowSettings);
        menu.Items.Add(new Separator());
        Item("Beenden", Quit);

        menu.Opened += (_, _) =>
        {
            onTop.IsChecked = Settings.AlwaysOnTop;
            walk.IsChecked = Settings.WalkAround;
        };
        return menu;
    }
}
