// The assistant statusLine command.
// Reads the session JSON from stdin, stores the rate-limit windows for the desktop pet
// and prints a short status line back to the assistant. Must never fail or hang.
// With --hook it is an assistant hook instead: it records the session's state (working,
// question, done) for the pet's ?/! marks, and the window showing the session, and prints nothing.
using System.Text;
using System.Text.Json;
using Claudius.Bridge;
using Claudius.Shared;

if (args.Length > 0 && args[0] == "--hook")
{
    try { RunHook(); }
    catch (Exception ex) { Log(ex.ToString()); }
    return 0; // never block or disturb the assistant
}

string line = "Claudius";
try
{
    line = Run();
}
catch (Exception ex)
{
    Log(ex.ToString());
}

using (var stdout = Console.OpenStandardOutput())
{
    var bytes = new UTF8Encoding(false).GetBytes(line + "\n");
    stdout.Write(bytes, 0, bytes.Length);
}
return 0;

static string Run()
{
    string input;
    using (var reader = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false)))
        input = reader.ReadToEnd();

    var now = DateTimeOffset.UtcNow;
    var previous = UsageStore.TryRead(DataPaths.UsageFile);

    RateWindow? fiveHour = null, sevenDay = null;
    string? model = null, sessionId = null;
    double? context = null;

    if (!string.IsNullOrWhiteSpace(input))
    {
        using var doc = JsonDocument.Parse(input);
        var root = doc.RootElement;

        model = ModelName(root);
        if (root.TryGetProperty("session_id", out var id) && id.ValueKind == JsonValueKind.String)
            sessionId = id.GetString();
        if (root.TryGetProperty("context_window", out var window) && window.ValueKind == JsonValueKind.Object
            && TryNumber(window, "used_percentage", out double used))
            context = used;

        if (root.TryGetProperty("rate_limits", out var limits) && limits.ValueKind == JsonValueKind.Object)
        {
            fiveHour = ParseWindow(limits, "five_hour");
            sevenDay = ParseWindow(limits, "seven_day");
        }
    }

    RememberContext(sessionId, context);

    if (fiveHour != null || sevenDay != null)
    {
        // A window can be missing (e.g. the assistant drops it once it reset) - keep the last known
        // value then; the app treats windows whose reset time has passed as 0 %.
        var snapshot = new UsageSnapshot
        {
            UpdatedAt = now.ToUnixTimeSeconds(),
            FiveHour = fiveHour ?? previous?.FiveHour,
            SevenDay = sevenDay ?? previous?.SevenDay,
            Model = model ?? previous?.Model,
        };
        UsageStore.WriteAtomic(DataPaths.UsageFile, snapshot);

        if (!SameValues(previous?.FiveHour, snapshot.FiveHour) || !SameValues(previous?.SevenDay, snapshot.SevenDay))
        {
            UsageStore.AppendHistory(new HistorySample
            {
                Time = snapshot.UpdatedAt,
                Session = snapshot.FiveHour?.UsedPercentage,
                SessionReset = snapshot.FiveHour?.ResetsAt,
                Week = snapshot.SevenDay?.UsedPercentage,
                WeekReset = snapshot.SevenDay?.ResetsAt,
            });
        }
        return Format(snapshot, model, now);
    }

    return previous != null
        ? Format(previous, model, now)
        : (model != null ? $"[{model}] " : "") + (German() ? "Claudius: warte auf Usage-Daten" : "Claudius: waiting for usage data");
}

// The model's display name ("Opus 4.1"), else its id; hook input may carry it as a plain string.
static string? ModelName(JsonElement root)
{
    if (!root.TryGetProperty("model", out var m)) return null;
    if (m.ValueKind == JsonValueKind.String) return m.GetString() is { Length: > 0 } plain ? plain : null;
    if (m.ValueKind != JsonValueKind.Object) return null;
    foreach (var key in new[] { "display_name", "id" })
    {
        if (m.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } name)
            return name;
    }
    return null;
}

// Stores how full the session's context window is, which makes the pet look sick. The status line runs often,
// so sessions.json is only written when the context changed by a whole percent (the sick levels are the app's
// settings); sessions the hooks do not know (yet) are left alone.
static void RememberContext(string? id, double? context)
{
    if (id is not { Length: > 0 } || context is not { } percent) return;
    try
    {
        if (!SessionStore.Read().TryGetValue(id, out var known)
            || known.ContextPercent is { } stored && Math.Abs(stored - percent) < 1) return;
        SessionStore.Update(sessions =>
        {
            if (sessions.TryGetValue(id, out var info)) info.ContextPercent = percent;
        });
    }
    catch (Exception ex)
    {
        Log(ex.ToString()); // the status line itself must still be printed
    }
}

// The app's UI language; English unless settings.json says German (saved before the choice existed: German too).
static bool German()
{
    try
    {
        if (!File.Exists(DataPaths.SettingsFile)) return false;
        using var doc = JsonDocument.Parse(File.ReadAllText(DataPaths.SettingsFile),
            new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        return !doc.RootElement.TryGetProperty("Language", out var language)
               || language.ValueKind != JsonValueKind.String
               || language.GetString() == "de";
    }
    catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
    {
        return false;
    }
}

static void RunHook()
{
    string input;
    using (var reader = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false)))
        input = reader.ReadToEnd();
    if (string.IsNullOrWhiteSpace(input)) return;

    using var doc = JsonDocument.Parse(input);
    var root = doc.RootElement;
    string? Text(string key) =>
        root.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    if (Text("session_id") is not { Length: > 0 } id) return;
    string hookEvent = Text("hook_event_name") ?? "";
    if (hookEvent == "SessionEnd")
    {
        // After /clear the assistant may carry on under a new id: keep the session a moment for it to take over.
        if (Text("reason") == "clear")
            SessionStore.Update(sessions =>
            {
                if (!sessions.TryGetValue(id, out var ended)) return;
                ended.State = SessionStates.Ended;
                ended.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            });
        else
            SessionStore.Update(sessions => sessions.Remove(id));
        return;
    }
    // /compact and /clear empty the context; the pet recovers.
    string? reset = hookEvent == "SessionStart" && Text("source") is "compact" or "clear" ? Text("source") : null;

    string? state = hookEvent switch
    {
        "SessionStart" => reset == "compact" ? null : SessionStates.Idle,
        "UserPromptSubmit" => SessionStates.Working,
        "Stop" => SessionStates.Done,
        "PermissionRequest" => SessionStates.Question,
        // Registered for the AskUserQuestion tool only
        "PreToolUse" => SessionStates.Question,
        "PostToolUse" => SessionStates.Working,
        "Notification" => Text("notification_type") is "permission_prompt" or "elicitation_dialog"
            or "elicitation_url_dialog" or "agent_needs_input" ? SessionStates.Question : null,
        _ => null,
    };
    if (state == null && reset == null) return;

    string? transcript = Text("transcript_path");
    long length = 0;
    try { if (transcript != null && File.Exists(transcript)) length = new FileInfo(transcript).Length; }
    catch (IOException) { }

    // The assistant tells its child processes where it runs: "cli" in a terminal, something else in the Desktop app.
    string? origin = Environment.GetEnvironmentVariable(AssistantCli.EntrypointVariable);

    // The window to bring up when the pet's "?" is clicked; looked for when the session starts
    // and again when it asks, which also picks up the current title for finding the Windows Terminal tab.
    var host = hookEvent == "SessionStart" || state == SessionStates.Question ? SessionHost.Find() : default;
    string? cwd = Text("cwd");

    SessionStore.Update(sessions =>
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!sessions.TryGetValue(id, out var info))
        {
            sessions[id] = info = new SessionInfo { State = SessionStates.Idle };
            // A new id after /clear: the session it replaces is the one /clear just ended in the same folder.
            if (reset == "clear" && cwd != null)
            {
                var previous = sessions
                    .Where(s => s.Value.State == SessionStates.Ended && s.Value.UpdatedAt >= now - 30
                                && string.Equals(s.Value.Cwd, cwd, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(s => s.Value.UpdatedAt)
                    .FirstOrDefault();
                if (previous.Value is { } old)
                {
                    sessions.Remove(previous.Key);
                    info.ContextPercent = old.ContextPercent;
                    info.Origin = old.Origin;
                    info.Window = old.Window;
                    info.WindowPid = old.WindowPid;
                    info.Title = old.Title;
                }
            }
        }
        info.Cwd = cwd ?? info.Cwd;
        if (!string.IsNullOrEmpty(origin)) info.Origin = origin;
        if (reset != null)
        {
            info.ContextBefore = info.ContextPercent;
            info.ContextPercent = 0;
            info.ContextResetAt = now;
            info.ContextReset = reset;
        }
        if (state != null) info.State = state;
        info.UpdatedAt = now;
        info.Transcript = transcript ?? info.Transcript;
        info.TranscriptLength = length;
        if (host.Window != IntPtr.Zero)
        {
            info.Window = (long)host.Window;
            info.WindowPid = host.Pid;
            info.Title = host.Title ?? info.Title;
        }
    });
}

static RateWindow? ParseWindow(JsonElement limits, string key)
{
    if (!limits.TryGetProperty(key, out var w) || w.ValueKind != JsonValueKind.Object) return null;
    if (!TryNumber(w, "used_percentage", out double pct)) return null;
    TryNumber(w, "resets_at", out double reset);
    return new RateWindow { UsedPercentage = pct, ResetsAt = (long)reset };
}

static bool TryNumber(JsonElement obj, string key, out double value)
{
    value = 0;
    if (!obj.TryGetProperty(key, out var p)) return false;
    if (p.ValueKind == JsonValueKind.Number) return p.TryGetDouble(out value);
    if (p.ValueKind == JsonValueKind.String)
    {
        string s = p.GetString() ?? "";
        if (double.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value)) return true;
        if (DateTimeOffset.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal, out var dto))
        {
            value = dto.ToUnixTimeSeconds();
            return true;
        }
    }
    return false;
}

static bool SameValues(RateWindow? a, RateWindow? b) =>
    a == null ? b == null : b != null && Math.Abs(a.UsedPercentage - b.UsedPercentage) < 0.01 && a.ResetsAt == b.ResetsAt;

static string Format(UsageSnapshot s, string? model, DateTimeOffset now)
{
    var parts = new List<string>();
    if (s.FiveHour != null) parts.Add(FormatWindow("Session", s.FiveHour, now, showReset: true));
    if (s.SevenDay != null) parts.Add(FormatWindow(German() ? "Woche" : "Week", s.SevenDay, now, showReset: false));
    return (model != null ? $"[{model}] " : "") + string.Join(" · ", parts);
}

static string FormatWindow(string label, RateWindow w, DateTimeOffset now, bool showReset)
{
    var left = w.ResetsAtTime - now;
    if (w.ResetsAt > 0 && left <= TimeSpan.Zero) return $"{label} 0%";
    string text = $"{label} {Math.Round(w.UsedPercentage):0}%";
    if (showReset && w.ResetsAt > 0)
        text += left.TotalHours >= 1 ? $" (↻ {(int)left.TotalHours}h {left.Minutes}m)" : $" (↻ {Math.Max(1, left.Minutes)}m)";
    return text;
}

static void Log(string message)
{
    try
    {
        Directory.CreateDirectory(DataPaths.DataDir);
        File.AppendAllText(DataPaths.LogFile, $"{DateTime.Now:O} [bridge] {message}\n");
    }
    catch { }
}
