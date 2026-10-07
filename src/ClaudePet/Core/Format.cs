using System.Globalization;

namespace ClaudePet.Core;

public static class Format
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    public static string Duration(TimeSpan span)
    {
        if (span < TimeSpan.FromMinutes(1)) return "< 1m";
        if (span.TotalDays >= 1) return $"{(int)span.TotalDays}T {span.Hours}h";
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h {span.Minutes}m";
        return $"{span.Minutes}m";
    }

    public static string Percent(double value) => Math.Round(value).ToString("0", German);

    /// <summary>"heute 16:30", "morgen 09:00" or "Sonntag 00:00".</summary>
    public static string DayTime(DateTimeOffset time, DateTimeOffset now)
    {
        var local = time.ToLocalTime();
        var today = now.ToLocalTime().Date;
        string clock = local.ToString("HH:mm", German);
        if (local.Date == today) return $"heute {clock}";
        if (local.Date == today.AddDays(1)) return $"morgen {clock}";
        if (local.Date < today.AddDays(7)) return $"{local.ToString("dddd", German)} {clock}";
        return local.ToString("dd.MM. HH:mm", German);
    }

    public static string Clock(DateTimeOffset time) => time.ToLocalTime().ToString("HH:mm", German);
}
