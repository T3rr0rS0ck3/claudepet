using System.Diagnostics;
using System.Globalization;
using System.Windows;
using ClaudePet.Core;
using ClaudePet.Shared;

namespace ClaudePet.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly App _app;

    public SettingsWindow(App app, AppSettings settings)
    {
        InitializeComponent();
        _app = app;
        _settings = settings;

        ScaleSlider.Value = settings.PetScale;
        AlwaysOnTopBox.IsChecked = settings.AlwaysOnTop;
        AnimationsBox.IsChecked = settings.Animations;
        AutostartBox.IsChecked = settings.StartWithWindows;
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

        RefreshSetupStatus();
    }

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
            MessageBox.Show(this, ex.Message, "Claude Pet", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        RefreshSetupStatus();
    }

    private void EditTexts_Click(object sender, RoutedEventArgs e)
    {
        _settings.Save();
        Process.Start(new ProcessStartInfo(DataPaths.SettingsFile) { UseShellExecute = true });
    }

    private void TestNotification_Click(object sender, RoutedEventArgs e) =>
        _app.ShowNotification("Claude Usage Pet", "So sehen Warnungen aus. 🟠");

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
            for (int i = 1; i < moods.Count; i++)
                if (moods[i] < moods[i - 1]) throw new FormatException("Zustandsgrenzen müssen aufsteigend sein.");

            _settings.PetScale = ScaleSlider.Value;
            _settings.AlwaysOnTop = AlwaysOnTopBox.IsChecked == true;
            _settings.Animations = AnimationsBox.IsChecked == true;
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

            _app.ApplySettings(save: true);
            Close();
        }
        catch (FormatException ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

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
