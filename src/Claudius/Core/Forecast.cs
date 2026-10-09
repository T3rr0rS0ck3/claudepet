using Claudius.Shared;

namespace Claudius.Core;

public static class Forecast
{
    /// <summary>
    /// Linear extrapolation of the recent usage trend within the current window.
    /// Returns the estimated time the window hits 100 %, or null if there is not enough data
    /// or usage is not rising.
    /// </summary>
    public static DateTimeOffset? EstimateLimit(IReadOnlyList<HistorySample> history, bool session,
        RateWindow window, DateTimeOffset now)
    {
        var lookback = session ? TimeSpan.FromMinutes(90) : TimeSpan.FromHours(48);
        var minSpan = session ? TimeSpan.FromMinutes(10) : TimeSpan.FromHours(2);
        long from = (now - lookback).ToUnixTimeSeconds();

        var points = new List<(long t, double p)>();
        foreach (var h in history)
        {
            double? p = session ? h.Session : h.Week;
            long? reset = session ? h.SessionReset : h.WeekReset;
            if (p == null || reset == null || h.Time < from) continue;
            // Same window (resets_at may jitter slightly between responses).
            if (Math.Abs(reset.Value - window.ResetsAt) > 600) continue;
            points.Add((h.Time, p.Value));
        }
        if (points.Count < 2) return null;

        var first = points[0];
        var last = points[^1];
        long span = last.t - first.t;
        if (span < minSpan.TotalSeconds) return null;

        double rate = (last.p - first.p) / span; // percent per second
        if (rate <= 1e-6) return null;

        double current = Math.Max(last.p, window.UsedPercentage);
        double secondsLeft = (100 - current) / rate;
        if (secondsLeft > TimeSpan.FromDays(30).TotalSeconds) return null;
        return DateTimeOffset.FromUnixTimeSeconds(last.t).AddSeconds(secondsLeft);
    }
}
