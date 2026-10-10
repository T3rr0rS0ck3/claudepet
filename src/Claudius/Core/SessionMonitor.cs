using System.IO;
using Claudius.Shared;

namespace Claudius.Core;

/// <summary>An assistant session as the pet shows it; <paramref name="Info"/> is what the bridge stored.</summary>
public sealed record SessionView(string Id, string Folder, string State, bool Desktop, SessionInfo Info)
{
    /// <summary>The folder, marked when the session runs in the desktop app.</summary>
    public string Label => Desktop ? Folder + " (Desktop)" : Folder;

    /// <summary>How sick the pet looks for this session's context window, 0..3.</summary>
    public int ContextLevel => ContextLevels.Of(Info.ContextPercent);
}

/// <summary>Reads sessions.json (written by the bridge's hooks) and reports state changes.</summary>
public sealed class SessionMonitor
{
    private DateTime _lastWrite;
    private Dictionary<string, SessionInfo> _stored = new();
    private Dictionary<string, (string State, int Context, long ContextResetAt)> _shown = new();
    private bool _loaded;

    public IReadOnlyList<SessionView> Sessions { get; private set; } = [];

    /// <summary>A session just started asking (state = question) or finished (state = done).</summary>
    public event Action<SessionView>? Attention;
    /// <summary>A session's context window just got full enough for sick level 2 (suggest /compact) or 3 (the grim reaper).</summary>
    public event Action<SessionView, int>? ContextRose;
    /// <summary>/compact or /clear (the kind) just emptied a session's context; the int is the sick level before.</summary>
    public event Action<SessionView, int, string>? ContextReset;
    /// <summary>The list, a state or a context level changed.</summary>
    public event Action? Changed;

    /// <summary>Called once a second; cheap when nothing changed.</summary>
    public void Poll()
    {
        try
        {
            var info = new FileInfo(DataPaths.SessionsFile);
            var write = info.Exists ? info.LastWriteTimeUtc : DateTime.MinValue;
            if (write != _lastWrite)
            {
                _lastWrite = write;
                _stored = SessionStore.Read();
            }
        }
        catch (IOException) { }

        long cutoff = DateTimeOffset.UtcNow.Add(-SessionStore.MaxAge).ToUnixTimeSeconds();
        var sessions = _stored
            .Where(s => s.Value.UpdatedAt >= cutoff && s.Value.State != SessionStates.Ended)
            .OrderBy(s => s.Value.UpdatedAt)
            .Select(s => new SessionView(s.Key, s.Value.Folder, EffectiveState(s.Value), s.Value.IsDesktop, s.Value))
            .ToList();

        var shown = sessions.ToDictionary(s => s.Id, s => (s.State, s.ContextLevel, s.Info.ContextResetAt));
        bool changed = shown.Count != _shown.Count || shown.Any(s => !_shown.TryGetValue(s.Key, out var old) || old != s.Value);
        if (!changed) return;

        if (_loaded)
        {
            foreach (var session in sessions)
            {
                bool known = _shown.TryGetValue(session.Id, out var old);
                bool entered = !known || old.State != session.State;
                if (entered && session.State is SessionStates.Question or SessionStates.Done) Attention?.Invoke(session);

                if (session.ContextLevel >= 2 && session.ContextLevel > (known ? old.Context : 0))
                    ContextRose?.Invoke(session, session.ContextLevel);
                // Only fresh resets: a session showing up long after its /compact gets no show.
                long resetAt = session.Info.ContextResetAt;
                int before = ContextLevels.Of(session.Info.ContextBefore);
                if (resetAt > (known ? old.ContextResetAt : 0) && resetAt >= DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 60
                    && before >= 1 && session.Info.ContextReset is { } kind)
                    ContextReset?.Invoke(session, before, kind);
            }
        }
        _loaded = true;
        _shown = shown;
        Sessions = sessions;
        Changed?.Invoke();
    }

    /// <summary>Question while any session waits for an answer, otherwise null.</summary>
    public string? Overall =>
        Sessions.Any(s => s.State == SessionStates.Question) ? SessionStates.Question : null;

    /// <summary>
    /// Some session is working and showed signs of life lately. Lets the pet look busy without status
    /// line data (the Desktop app may not run it); the time limit keeps a crashed session from counting forever.
    /// </summary>
    public bool AnyWorking
    {
        get
        {
            var recent = DateTime.UtcNow - BusyTimeout;
            return _stored.Values.Any(s => EffectiveState(s) == SessionStates.Working && LastSign(s) >= recent);
        }
    }

    private static readonly TimeSpan BusyTimeout = TimeSpan.FromMinutes(3);

    private static DateTime LastSign(SessionInfo info)
    {
        var updated = DateTimeOffset.FromUnixTimeSeconds(info.UpdatedAt).UtcDateTime;
        try
        {
            if (info.Transcript is { Length: > 0 } path && File.Exists(path))
            {
                var written = File.GetLastWriteTimeUtc(path);
                if (written > updated) return written;
            }
        }
        catch (IOException) { }
        return updated;
    }

    private static string EffectiveState(SessionInfo info)
    {
        // No hook reports an answered permission prompt; the conversation log growing again means
        // The assistant got its answer and carries on.
        if (info.State == SessionStates.Question && info.Transcript is { Length: > 0 } path)
        {
            try
            {
                var file = new FileInfo(path);
                if (file.Exists && file.Length > info.TranscriptLength) return SessionStates.Working;
            }
            catch (IOException) { }
        }
        return info.State;
    }
}
