using ClaudePet.Shared;

namespace ClaudePet.Core;

/// <summary>Ordered by severity.</summary>
public enum PetMood { Unknown, Relaxed, Normal, Attentive, Nervous, Worried, Panic, Exhausted }

/// <summary>Usage as the app sees it right now (expired windows count as reset).</summary>
public sealed class UsageState
{
    public UsageSnapshot? Snapshot { get; init; }
    public double? Session { get; init; }
    public double? Week { get; init; }
    public bool SessionExpired { get; init; }
    public bool WeekExpired { get; init; }
    public PetMood Mood { get; init; }
    /// <summary>The highest relevant usage value that determines the mood.</summary>
    public double? Max { get; init; }
    /// <summary>Claude Code reported something very recently, i.e. Claude is probably working.</summary>
    public bool Active { get; init; }

    public static UsageState Compute(UsageSnapshot? snapshot, MoodThresholds thresholds, DateTimeOffset now)
    {
        var (session, sessionExpired) = Effective(snapshot?.FiveHour, now);
        var (week, weekExpired) = Effective(snapshot?.SevenDay, now);
        double? max = session.HasValue || week.HasValue ? Math.Max(session ?? 0, week ?? 0) : null;

        return new UsageState
        {
            Snapshot = snapshot,
            Session = session,
            Week = week,
            SessionExpired = sessionExpired,
            WeekExpired = weekExpired,
            Max = max,
            Mood = max.HasValue ? MoodFor(max.Value, thresholds) : PetMood.Unknown,
            Active = snapshot != null && now - snapshot.UpdatedAtTime < TimeSpan.FromSeconds(20),
        };
    }

    public static PetMood MoodFor(double percent, MoodThresholds t)
    {
        double p = Math.Floor(percent);
        if (p >= t.Exhausted) return PetMood.Exhausted;
        if (p >= t.Panic) return PetMood.Panic;
        if (p >= t.Worried) return PetMood.Worried;
        if (p >= t.Nervous) return PetMood.Nervous;
        if (p >= t.Attentive) return PetMood.Attentive;
        if (p >= t.Normal) return PetMood.Normal;
        return PetMood.Relaxed;
    }

    private static (double?, bool) Effective(RateWindow? w, DateTimeOffset now)
    {
        if (w == null) return (null, false);
        if (w.ResetsAt > 0 && now >= w.ResetsAtTime) return (0, true);
        return (Math.Clamp(w.UsedPercentage, 0, 100), false);
    }
}
