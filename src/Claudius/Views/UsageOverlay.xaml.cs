using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Claudius.Core;
using Claudius.Shared;

namespace Claudius.Views;

public partial class UsageOverlay : Window
{
    private static readonly Brush EmptySegment = Freeze(new SolidColorBrush(Color.FromRgb(0x3A, 0x39, 0x36)));

    public UsageOverlay()
    {
        InitializeComponent();
        BuildBar(SessionBar);
        BuildBar(WeekBar);
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    public void Update(UsageState state, IReadOnlyList<HistorySample> history, AppSettings settings,
        SetupStatus setup, DateTimeOffset now)
    {
        var snapshot = state.Snapshot;
        var t = settings.Thresholds;

        // Session
        SetBar(SessionBar, SessionPct, state.Session, t);
        if (snapshot?.FiveHour is { } session)
        {
            SessionReset.Text = state.SessionExpired
                ? Strings.ResetWaiting
                : Strings.SessionResetIn(Format.Duration(session.ResetsAtTime - now), Format.Clock(session.ResetsAtTime));
            SetForecast(SessionForecast, settings.ShowForecast && !state.SessionExpired, history, true, session, now,
                eta => Strings.LimitIn(Format.Duration(eta - now)));
        }
        else
        {
            SessionReset.Text = Strings.NoData;
            SessionForecast.Visibility = Visibility.Collapsed;
        }

        // Week
        SetBar(WeekBar, WeekPct, state.Week, t);
        if (snapshot?.SevenDay is { } week)
        {
            WeekReset.Text = state.WeekExpired
                ? Strings.ResetWaiting
                : Strings.WeekResetAt(Format.DayTime(week.ResetsAtTime, now));
            SetForecast(WeekForecast, settings.ShowForecast && !state.WeekExpired, history, false, week, now,
                eta => Strings.LimitAt(Format.DayTime(eta, now)));
        }
        else
        {
            WeekReset.Text = Strings.NoData;
            WeekForecast.Visibility = Visibility.Collapsed;
        }

        StateText.Text = Strings.MoodLabel(state.Mood);
        StateDot.Fill = state.Max is { } max ? Palette.ForPercent(max, t) : Palette.Neutral;

        if (snapshot != null)
        {
            var age = now - snapshot.UpdatedAtTime;
            UpdatedText.Text = (age < TimeSpan.FromMinutes(1) ? Strings.UpdatedJustNow : Strings.UpdatedAgo(Format.Duration(age)))
                               + (snapshot.Model != null ? $" · {snapshot.Model}" : "");
        }
        else
        {
            UpdatedText.Text = "";
        }

        string? hint = setup switch
        {
            SetupStatus.NotConfigured or SetupStatus.OtherStatusLine => Strings.HintNotConnected,
            SetupStatus.BridgeMissing => Strings.HintBridgeMissing,
            _ when snapshot == null => Strings.HintFirstReply,
            _ when now - snapshot.UpdatedAtTime > TimeSpan.FromHours(5) => Strings.HintStale,
            _ => null,
        };
        HintText.Text = hint ?? "";
        HintText.Visibility = hint == null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Shows the overlay next to the pet, above it if there is room.</summary>
    public void ShowNear(Rect pet, Window owner)
    {
        Left = -10000;
        Show();
        UpdateLayout();

        var dpi = VisualTreeHelper.GetDpi(owner);
        var center = new System.Drawing.Point((int)((pet.Left + pet.Width / 2) * dpi.DpiScaleX), (int)(pet.Top * dpi.DpiScaleY));
        var wa = System.Windows.Forms.Screen.FromPoint(center).WorkingArea;
        var area = new Rect(wa.Left / dpi.DpiScaleX, wa.Top / dpi.DpiScaleY, wa.Width / dpi.DpiScaleX, wa.Height / dpi.DpiScaleY);

        double left = pet.Left + pet.Width / 2 - ActualWidth / 2;
        double top = pet.Top - ActualHeight;
        if (top < area.Top) top = pet.Bottom;
        Left = Math.Clamp(left, area.Left, Math.Max(area.Left, area.Right - ActualWidth));
        Top = Math.Clamp(top, area.Top, Math.Max(area.Top, area.Bottom - ActualHeight));
        Activate();
    }

    private static void BuildBar(UniformGrid bar)
    {
        for (int i = 0; i < bar.Columns; i++)
            bar.Children.Add(new Rectangle { Margin = new Thickness(1, 0, 1, 0), Fill = EmptySegment });
    }

    private static void SetBar(UniformGrid bar, TextBlock label, double? percent, MoodThresholds t)
    {
        int filled = percent is { } p ? (int)Math.Round(p / 100 * bar.Columns) : 0;
        if (percent > 0 && filled == 0) filled = 1;
        var brush = percent is { } value ? Palette.ForPercent(value, t) : Palette.Neutral;
        for (int i = 0; i < bar.Children.Count; i++)
            ((Rectangle)bar.Children[i]).Fill = i < filled ? brush : EmptySegment;
        label.Text = Strings.Percent(percent is { } v ? Format.Percent(v) : "–");
    }

    private static void SetForecast(TextBlock target, bool enabled, IReadOnlyList<HistorySample> history,
        bool session, RateWindow window, DateTimeOffset now, Func<DateTimeOffset, string> describe)
    {
        var eta = enabled ? Forecast.EstimateLimit(history, session, window, now) : null;
        if (eta == null)
        {
            target.Visibility = Visibility.Collapsed;
            return;
        }
        target.Visibility = Visibility.Visible;
        target.Text = eta < window.ResetsAtTime ? Strings.Forecast(describe(eta.Value)) : Strings.ForecastLasts;
    }

    private bool _closing;

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        if (!_closing) Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _closing = true;
        base.OnClosing(e);
    }

    private void CloseButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_closing) Close();
    }

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
