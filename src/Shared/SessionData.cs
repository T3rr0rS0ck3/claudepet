using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace Claudius.Shared;

/// <summary>What an assistant session is doing, as far as its hooks tell.</summary>
public static class SessionStates
{
    /// <summary>Started, waiting for the first prompt.</summary>
    public const string Idle = "idle";
    public const string Working = "working";
    /// <summary>The assistant asks something or waits for a permission.</summary>
    public const string Question = "question";
    /// <summary>The assistant finished its turn.</summary>
    public const string Done = "done";
}

/// <summary>One the assistant session, written by the bridge's hook mode and read by the app.</summary>
public sealed class SessionInfo
{
    [JsonPropertyName("cwd")] public string? Cwd { get; set; }
    [JsonPropertyName("state")] public string State { get; set; } = SessionStates.Working;
    /// <summary>Unix epoch seconds of the last hook call.</summary>
    [JsonPropertyName("updated_at")] public long UpdatedAt { get; set; }
    /// <summary>
    /// The assistant's conversation log and its size when the state was set. No hook reports an
    /// answered permission prompt, so a question counts as answered once the log grows again.
    /// </summary>
    [JsonPropertyName("transcript")] public string? Transcript { get; set; }
    [JsonPropertyName("transcript_length")] public long TranscriptLength { get; set; }
    /// <summary>The assistant's <see cref="AssistantCli.EntrypointVariable"/>: "cli" in a terminal, something else in the desktop app.</summary>
    [JsonPropertyName("origin")] public string? Origin { get; set; }
    /// <summary>The session's model as the status line reports it (e.g. "Opus 4.1"), for its baby pet's outfit.</summary>
    [JsonPropertyName("model")] public string? Model { get; set; }
    /// <summary>
    /// Window that shows the session (console, Windows Terminal or Desktop app) and the process it belonged
    /// to, so a reused handle is not mistaken for it; 0 if unknown (e.g. saved by an older bridge).
    /// </summary>
    [JsonPropertyName("window")] public long Window { get; set; }
    [JsonPropertyName("window_pid")] public int WindowPid { get; set; }
    /// <summary>The session's console title when it last asked something: finds its tab in Windows Terminal.</summary>
    [JsonPropertyName("title")] public string? Title { get; set; }

    [JsonIgnore] public bool IsDesktop => Origin?.Contains("desktop", StringComparison.OrdinalIgnoreCase) == true;
    [JsonIgnore] public string Folder => Cwd is { Length: > 0 } cwd ? Path.GetFileName(cwd.TrimEnd('\\', '/')) : "Session";
}

/// <summary>sessions.json: session id → state. Several sessions write it, so changes are serialized.</summary>
public static class SessionStore
{
    /// <summary>Sessions without a hook call for this long are dropped (closed terminal without SessionEnd).</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(12);

    private const string MutexName = "Claudius.Sessions.v1";

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static Dictionary<string, SessionInfo> Read()
    {
        try
        {
            if (!File.Exists(DataPaths.SessionsFile)) return new();
            return JsonSerializer.Deserialize<Dictionary<string, SessionInfo>>(
                File.ReadAllText(DataPaths.SessionsFile, Encoding.UTF8), Options) ?? new();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new();
        }
    }

    /// <summary>Applies <paramref name="change"/> to the stored sessions under a machine-wide lock.</summary>
    public static void Update(Action<Dictionary<string, SessionInfo>> change)
    {
        using var mutex = new Mutex(false, MutexName);
        bool owned = false;
        try
        {
            try { owned = mutex.WaitOne(TimeSpan.FromSeconds(2)); }
            catch (AbandonedMutexException) { owned = true; }
            if (!owned) return; // never block the assistant

            var sessions = Read();
            change(sessions);
            long cutoff = DateTimeOffset.UtcNow.Add(-MaxAge).ToUnixTimeSeconds();
            foreach (var id in sessions.Where(s => s.Value.UpdatedAt < cutoff).Select(s => s.Key).ToList())
                sessions.Remove(id);

            Directory.CreateDirectory(DataPaths.DataDir);
            string temp = DataPaths.SessionsFile + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(sessions, Options), new UTF8Encoding(false));
            File.Move(temp, DataPaths.SessionsFile, overwrite: true);
        }
        finally
        {
            if (owned) mutex.ReleaseMutex();
        }
    }
}
