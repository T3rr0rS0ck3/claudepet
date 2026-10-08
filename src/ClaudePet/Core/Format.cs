using System.Globalization;

namespace ClaudePet.Core;

/// <summary>Durations, percentages and times in the current UI language (<see cref="Strings"/>).</summary>
public static class Format
{
    private static bool German => Strings.Current == Strings.German;

    public static string Duration(TimeSpan span)
    {
        if (span < TimeSpan.FromMinutes(1)) return "< 1m";
        if (span.TotalDays >= 1) return $"{(int)span.TotalDays}{(German ? "T" : "d")} {span.Hours}h";
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h {span.Minutes}m";
        return $"{span.Minutes}m";
    }

    public static string Percent(double value) => Math.Round(value).ToString("0", CultureInfo.InvariantCulture);

    /// <summary>"today 16:30", "tomorrow 09:00", "Sunday 00:00" or "12 Oct 16:30" (German: "heute 16:30", …).</summary>
    public static string DayTime(DateTimeOffset time, DateTimeOffset now)
    {
        var culture = Strings.Culture;
        var local = time.ToLocalTime();
        var today = now.ToLocalTime().Date;
        string clock = local.ToString("HH:mm", culture);
        if (local.Date == today) return (German ? "heute " : "today ") + clock;
        if (local.Date == today.AddDays(1)) return (German ? "morgen " : "tomorrow ") + clock;
        if (local.Date < today.AddDays(7)) return $"{local.ToString("dddd", culture)} {clock}";
        return local.ToString(German ? "dd.MM. HH:mm" : "d MMM HH:mm", culture);
    }

    public static string Clock(DateTimeOffset time) => time.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
}
