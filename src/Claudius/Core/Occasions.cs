using System.Diagnostics;
using System.Globalization;
using Claudius.Pet;

namespace Claudius.Core;

/// <summary>Which accessory the pet wears right now, by the time of day and the calendar.</summary>
public static class Occasions
{
    private static readonly DateTime? FakeStart = ReadFakeNow();
    private static readonly Stopwatch Since = Stopwatch.StartNew();

    /// <summary>
    /// The current time; CLAUDIUS_FAKE_NOW (e.g. 2026-12-24T23:30) starts the clock at another moment,
    /// for trying out the accessories.
    /// </summary>
    public static DateTime Now => FakeStart is { } start ? start + Since.Elapsed : DateTime.Now;

    /// <summary>
    /// Night first, then the occasions: birthday, New Year's Eve and New Year's Day, Christmas (1–26 Dec),
    /// Easter (Good Friday to Easter Monday), Halloween (all of October).
    /// </summary>
    public static Accessory For(DateTime now, AppSettings settings)
    {
        if (IsNight(now, settings)) return Accessory.Nightcap;
        if (!settings.SeasonalAccessories) return Accessory.None;

        var day = now.Date;
        if (TryParseDay(settings.Birthday, out var birthday) && birthday == (day.Month, day.Day)) return Accessory.PartyHat;
        if (day is { Month: 12, Day: 31 } or { Month: 1, Day: 1 }) return Accessory.PartyHat;
        if (day is { Month: 12, Day: <= 26 }) return Accessory.SantaHat;
        var easter = EasterSunday(day.Year);
        if (day >= easter.AddDays(-2) && day <= easter.AddDays(1)) return Accessory.BunnyEars;
        if (day.Month == 10) return Accessory.WitchHat;
        return Accessory.None;
    }

    /// <summary>Within the night hours; a night from 23:00 to 06:00 spans midnight.</summary>
    public static bool IsNight(DateTime now, AppSettings settings)
    {
        if (!settings.NightMode || !TryParseTime(settings.NightStart, out var start) || !TryParseTime(settings.NightEnd, out var end))
            return false;
        var time = now.TimeOfDay;
        return start <= end ? time >= start && time < end : time >= start || time < end;
    }

    /// <summary>Easter Sunday (Gregorian), by the anonymous Gregorian algorithm (Meeus/Jones/Butcher).</summary>
    public static DateTime EasterSunday(int year)
    {
        int a = year % 19, b = year / 100, c = year % 100, d = b / 4, e = b % 4;
        int f = (b + 8) / 25, g = (b - f + 1) / 3, h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7, m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31, day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateTime(year, month, day);
    }

    /// <summary>"HH:mm", e.g. "23:00".</summary>
    public static bool TryParseTime(string? text, out TimeSpan time) =>
        TimeSpan.TryParseExact(text, @"hh\:mm", CultureInfo.InvariantCulture, out time) && time < TimeSpan.FromDays(1);

    /// <summary>"MM-dd", e.g. "03-14"; 29 February counts too.</summary>
    public static bool TryParseDay(string? text, out (int Month, int Day) day)
    {
        day = default;
        if (!DateTime.TryParseExact("2000-" + text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return false;
        day = (date.Month, date.Day);
        return true;
    }

    private static DateTime? ReadFakeNow() =>
        DateTime.TryParse(Environment.GetEnvironmentVariable("CLAUDIUS_FAKE_NOW"), CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var time) ? time : null;
}
