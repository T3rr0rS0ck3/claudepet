using System.IO;
using ClaudePet.Shared;

namespace ClaudePet.Core;

/// <summary>A Claude Code session as the pet shows it.</summary>
public sealed record SessionView(string Id, string Folder, string State);

/// <summary>Reads sessions.json (written by the bridge's hooks) and reports state changes.</summary>
public sealed class SessionMonitor
{
    private DateTime _lastWrite;
    private Dictionary<string, SessionInfo> _stored = new();
    private Dictionary<string, string> _shown = new();
    private bool _loaded;

    public IReadOnlyList<SessionView> Sessions { get; private set; } = [];

    /// <summary>A session just started asking (state = question) or finished (state = done).</summary>
    public event Action<SessionView>? Attention;
    /// <summary>The list or a state changed.</summary>
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
            .Where(s => s.Value.UpdatedAt >= cutoff)
            .OrderBy(s => s.Value.UpdatedAt)
            .Select(s => new SessionView(s.Key, s.Value.Folder, EffectiveState(s.Value)))
            .ToList();

        var shown = sessions.ToDictionary(s => s.Id, s => s.State);
        bool changed = shown.Count != _shown.Count || shown.Any(s => !_shown.TryGetValue(s.Key, out var old) || old != s.Value);
        if (!changed) return;

        if (_loaded)
        {
            foreach (var session in sessions)
            {
                bool entered = !_shown.TryGetValue(session.Id, out var old) || old != session.State;
                if (entered && session.State is SessionStates.Question or SessionStates.Done) Attention?.Invoke(session);
            }
        }
        _loaded = true;
        _shown = shown;
        Sessions = sessions;
        Changed?.Invoke();
    }

    /// <summary>The most urgent state over all sessions: a question beats done.</summary>
    public string? Overall =>
        Sessions.Any(s => s.State == SessionStates.Question) ? SessionStates.Question
        : Sessions.Any(s => s.State == SessionStates.Done) ? SessionStates.Done
        : null;

    private static string EffectiveState(SessionInfo info)
    {
        // No hook reports an answered permission prompt; the conversation log growing again means
        // Claude got its answer and carries on.
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
