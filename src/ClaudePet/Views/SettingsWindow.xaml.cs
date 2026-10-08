using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using ClaudePet.Core;
using ClaudePet.Pet;
using ClaudePet.Shared;
using Forms = System.Windows.Forms;

namespace ClaudePet.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly App _app;

    public SettingsWindow(App app, AppSettings settings)
    {
        InitializeComponent();
        // Scroll instead of growing past the screen; Escape closes like a normal dialog.
        MaxHeight = SystemParameters.WorkArea.Height - 20;
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); };
        _app = app;
        _settings = settings;

        ScaleSlider.Value = settings.PetScale;
        ColorBox.Text = settings.PetColor;
        ClaudeMascotBox.IsChecked = settings.ClaudeMascotColor;
        AlwaysOnTopBox.IsChecked = settings.AlwaysOnTop;
        AnimationsBox.IsChecked = settings.Animations;
        WalkAroundBox.IsChecked = settings.WalkAround;
        CrossMonitorsBox.IsChecked = settings.CrossMonitors;
        EmotesBox.IsChecked = settings.Emotes;
        UpdatesBox.IsChecked = settings.CheckForUpdates;
        VersionText.Text = "Installierte Version: " + Updater.Format(Updater.CurrentVersion)
            + (Updater.IsDevBuild ? " (selbst gebaut, keine Updates)" : Updater.IsInstalled ? "" : " (portabel)");
        CheckUpdateButton.IsEnabled = !Updater.IsDevBuild;
        ShowUpdate(app.AvailableUpdate);
        if (AppPackage.IsPackaged)
        {
            VersionText.Text = $"Version {Updater.Format(Updater.CurrentVersion)} – Updates kommen über den Microsoft Store.";
            UpdatesBox.Visibility = UpdateButtons.Visibility = UpdateStatusText.Visibility = Visibility.Collapsed;
        }
        SessionMarksBox.IsChecked = settings.SessionMarks;
        SessionPetsBox.IsChecked = settings.SessionPets;
        AutostartBox.IsChecked = settings.StartWithWindows;
        // Store version: switched off under Settings → Apps → Startup, only the user can switch it on there.
        if (Autostart.BlockedByUser) AutostartBlockedPanel.Visibility = Visibility.Visible;
        IntervalBox.Text = settings.PollIntervalSeconds.ToString(CultureInfo.InvariantCulture);
        SessionWarnBox.Text = string.Join(", ", settings.SessionWarnThresholds);
        WeekWarnBox.Text = string.Join(", ", settings.WeekWarnThresholds);
        var t = settings.Thresholds;
        MoodThresholdsBox.Text = string.Join(", ", t.Normal, t.Attentive, t.Nervous, t.Worried, t.Panic, t.Exhausted);
        ForecastBox.IsChecked = settings.ShowForecast;
        BubblesBox.IsChecked = settings.SpeechBubbles;
        BubbleDurationBox.Text = settings.BubbleDurationSeconds.ToString(CultureInfo.InvariantCulture);
        NameBox.Text = settings.UserName;
        NotificationsBox.IsChecked = settings.Notifications;
        ReposPathBox.Text = settings.ReposPath ?? "";
        TerminalBox.ItemsSource = TerminalChoices;
        TerminalBox.DisplayMemberPath = "Value";
        TerminalBox.SelectedValuePath = "Key";
        TerminalBox.SelectedValue = settings.Terminal;
        GhostDragBox.IsChecked = settings.GhostDrag;
        VoiceBox.IsChecked = settings.VoiceChat;

        string? claude = ClaudeLauncher.FindClaude();
        ClaudeStatusText.Text = claude != null
            ? "Claude Code gefunden: " + claude
            : "⚠ Claude Code wurde nicht gefunden (claude ist nicht im PATH).";

        RefreshSetupStatus();
    }

    private static readonly KeyValuePair<TerminalKind, string>[] TerminalChoices =
    [
        new(TerminalKind.Auto, ClaudeLauncher.HasWindowsTerminal ? "Automatisch (Windows Terminal)" : "Automatisch (cmd)"),
        new(TerminalKind.WindowsTerminal, "Windows Terminal"),
        new(TerminalKind.Cmd, "Eingabeaufforderung (cmd)"),
        new(TerminalKind.PowerShell, "PowerShell"),
        new(TerminalKind.Desktop, "Claude Desktop (Code-Tab)"),
    ];

    private void BrowseRepos_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Ordner mit deinen Projekten wählen" };
        if (Directory.Exists(ReposPathBox.Text)) dialog.InitialDirectory = ReposPathBox.Text;
        if (dialog.ShowDialog(this) == true) ReposPathBox.Text = dialog.FolderName;
    }

    private void ColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        bool valid = Sprite.TryParseColor(ColorBox.Text, out uint argb);
        ColorPreview.Background = valid ? new SolidColorBrush(ToColor(argb)) : Brushes.Transparent;
        // Show the color on the pet right away; closing without saving goes back to the saved one.
        if (valid && IsLoaded) _app.PreviewPetColor(argb);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _app.PreviewPetColor(null);
    }

    private void PickColor_Click(object sender, RoutedEventArgs e)
    {
        if (!Sprite.TryParseColor(ColorBox.Text, out uint argb)) argb = Sprite.DefaultBodyColor;
        using var dialog = new Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(unchecked((int)argb)),
        };
        if (dialog.ShowDialog() == Forms.DialogResult.OK)
            ColorBox.Text = Sprite.ToHex(unchecked((uint)dialog.Color.ToArgb()));
    }

    private void DefaultColor_Click(object sender, RoutedEventArgs e) => ColorBox.Text = AppSettings.DefaultPetColor;

    private static Color ToColor(uint argb) =>
        Color.FromRgb((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

    private void RefreshSetupStatus()
    {
        var status = ClaudeCodeSetup.GetStatus();
        SetupStatusText.Text = status switch
        {
            SetupStatus.Connected => "✔ Verbunden – ClaudePetBridge ist als statusLine eingetragen.",
            SetupStatus.OtherStatusLine => "Es ist bereits eine andere statusLine eingetragen:\n" + ClaudeCodeSetup.CurrentCommand(),
            SetupStatus.BridgeMissing => "ClaudePetBridge.exe wurde nicht gefunden (muss neben ClaudePet.exe liegen).",
            _ => "Nicht verbunden.",
        };
        ConnectButton.IsEnabled = status is SetupStatus.NotConfigured or SetupStatus.OtherStatusLine;
        DisconnectButton.IsEnabled = status == SetupStatus.Connected;
    }

    private void Connect_Click(object sender, RoutedEventArgs e)
    {
        _app.ConnectClaudeCode(this);
        RefreshSetupStatus();
    }

    private void Disconnect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ClaudeCodeSetup.Uninstall();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Claudius", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        RefreshSetupStatus();
    }

    private void EditTexts_Click(object sender, RoutedEventArgs e)
    {
        _settings.Save();
        Process.Start(new ProcessStartInfo(DataPaths.SettingsFile) { UseShellExecute = true });
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdateButton.IsEnabled = false;
        UpdateStatusText.Text = "Suche…";
        UpdateStatusText.Visibility = Visibility.Visible;
        try
        {
            var update = await _app.CheckForUpdateAsync(silent: false);
            ShowUpdate(update);
            if (update == null) UpdateStatusText.Text = "✔ Du hast die neueste Version.";
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = "Suche fehlgeschlagen: " + ex.Message;
        }
        finally
        {
            CheckUpdateButton.IsEnabled = true;
        }
    }

    private void ShowUpdate(UpdateInfo? update)
    {
        InstallUpdateButton.Visibility = update != null ? Visibility.Visible : Visibility.Collapsed;
        if (update == null) return;
        InstallUpdateButton.Content = Updater.IsInstalled ? "Installieren" : "Herunterladen";
        UpdateStatusText.Text = $"Version {update.VersionText} ist verfügbar.";
        UpdateStatusText.Visibility = Visibility.Visible;
    }

    private void StartupSettings_Click(object sender, RoutedEventArgs e) => App.OpenUrl("ms-settings:startupapps");

    private void InstallUpdate_Click(object sender, RoutedEventArgs e) => _app.InstallUpdate(this);

    private void TestNotification_Click(object sender, RoutedEventArgs e) =>
        _app.ShowNotification("Claudius", "So sehen Warnungen aus. 🟠");

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int interval = ParseInt(IntervalBox.Text, "Aktualisierungsintervall");
            double duration = double.Parse(BubbleDurationBox.Text.Replace(',', '.'), CultureInfo.InvariantCulture);
            var sessionWarn = ParseList(SessionWarnBox.Text, "Session-Warnschwellen");
            var weekWarn = ParseList(WeekWarnBox.Text, "Wochen-Warnschwellen");
            var moods = ParseList(MoodThresholdsBox.Text, "Zustandsgrenzen");
            if (moods.Count != 6) throw new FormatException("Zustandsgrenzen: genau 6 Werte angeben.");
            if (!Sprite.TryParseColor(ColorBox.Text, out uint petColor))
                throw new FormatException("Farbe: bitte als #RRGGBB angeben, z. B. #D97757.");
            string reposPath = ReposPathBox.Text.Trim().Trim('"');
            if (reposPath.Length > 0 && !Directory.Exists(reposPath))
                throw new FormatException("Repo-Ordner existiert nicht.");

            // Switching voice chat off also switches Claude Code's dictation off again. That setting lives in
            // Claude Code's own settings.json, so write it first: if it fails, nothing is applied.
            if (_settings.VoiceChat && VoiceBox.IsChecked != true)
            {
                try
                {
                    ClaudeCodeSetup.SetVoiceEnabled(false);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
                                               or System.Text.Json.JsonException)
                {
                    throw new FormatException("Claude-Code-Einstellungen konnten nicht geschrieben werden: " + ex.Message);
                }
            }
            for (int i = 1; i < moods.Count; i++)
                if (moods[i] < moods[i - 1]) throw new FormatException("Zustandsgrenzen müssen aufsteigend sein.");

            _settings.PetScale = ScaleSlider.Value;
            _settings.PetColor = Sprite.ToHex(petColor);
            _settings.ClaudeMascotColor = ClaudeMascotBox.IsChecked == true;
            _settings.AlwaysOnTop = AlwaysOnTopBox.IsChecked == true;
            _settings.Animations = AnimationsBox.IsChecked == true;
            _settings.WalkAround = WalkAroundBox.IsChecked == true;
            _settings.CrossMonitors = CrossMonitorsBox.IsChecked == true;
            _settings.Emotes = EmotesBox.IsChecked == true;
            _settings.CheckForUpdates = UpdatesBox.IsChecked == true;
            _settings.SessionMarks = SessionMarksBox.IsChecked == true;
            _settings.SessionPets = SessionPetsBox.IsChecked == true;
            _settings.StartWithWindows = AutostartBox.IsChecked == true;
            _settings.PollIntervalSeconds = Math.Clamp(interval, 1, 300);
            _settings.SessionWarnThresholds = sessionWarn;
            _settings.WeekWarnThresholds = weekWarn;
            _settings.Thresholds = new MoodThresholds
            {
                Normal = moods[0], Attentive = moods[1], Nervous = moods[2],
                Worried = moods[3], Panic = moods[4], Exhausted = moods[5],
            };
            _settings.ShowForecast = ForecastBox.IsChecked == true;
            _settings.SpeechBubbles = BubblesBox.IsChecked == true;
            _settings.BubbleDurationSeconds = Math.Clamp(duration, 1, 60);
            _settings.UserName = NameBox.Text.Trim();
            _settings.Notifications = NotificationsBox.IsChecked == true;
            _settings.ReposPath = reposPath.Length > 0 ? reposPath : null;
            _settings.Terminal = TerminalBox.SelectedValue is TerminalKind terminal ? terminal : TerminalKind.Auto;
            _settings.GhostDrag = GhostDragBox.IsChecked == true;
            _settings.VoiceChat = VoiceBox.IsChecked == true;

            _app.ApplySettings(save: true);
            Close();
        }
        catch (FormatException ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    // Borderless like the usage overlay: drag by the title, close with the ✕.
    private void Title_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        try { DragMove(); } catch (InvalidOperationException) { }
    }

    private void Close_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => Close();

    // IsCancel only closes modal dialogs; this window is shown non-modally.
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private static int ParseInt(string text, string field) =>
        int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : throw new FormatException($"{field}: ungültige Zahl.");

    private static List<int> ParseList(string text, string field) =>
        text.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => Math.Clamp(ParseInt(part, field), 0, 100))
            .ToList();
}
