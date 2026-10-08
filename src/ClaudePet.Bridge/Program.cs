// Claude Code statusLine command.
// Reads the session JSON from stdin, stores the rate-limit windows for the desktop pet
// and prints a short status line back to Claude Code. Must never fail or hang.
// With --hook it is a Claude Code hook instead: it records the session's state (working,
// question, done) for the pet's ?/! marks and prints nothing.
using System.Text;
using System.Text.Json;
using ClaudePet.Shared;

if (args.Length > 0 && args[0] == "--hook")
{
    try { RunHook(); }
    catch (Exception ex) { Log(ex.ToString()); }
    return 0; // never block or disturb Claude Code
}

string line = "Claude Pet";
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
    string? model = null;

    if (!string.IsNullOrWhiteSpace(input))
    {
        using var doc = JsonDocument.Parse(input);
        var root = doc.RootElement;

        if (root.TryGetProperty("model", out var m) && m.ValueKind == JsonValueKind.Object &&
            m.TryGetProperty("display_name", out var name) && name.ValueKind == JsonValueKind.String)
            model = name.GetString();

        if (root.TryGetProperty("rate_limits", out var limits) && limits.ValueKind == JsonValueKind.Object)
        {
            fiveHour = ParseWindow(limits, "five_hour");
            sevenDay = ParseWindow(limits, "seven_day");
        }
    }

    if (fiveHour != null || sevenDay != null)
    {
        // A window can be missing (e.g. Claude Code drops it once it reset) - keep the last known
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
        : (model != null ? $"[{model}] " : "") + "Claude Pet: warte auf Usage-Daten";
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
        SessionStore.Update(sessions => sessions.Remove(id));
        return;
    }

    string? state = hookEvent switch
    {
        "SessionStart" => SessionStates.Idle,
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
    if (state == null) return;

    string? transcript = Text("transcript_path");
    long length = 0;
    try { if (transcript != null && File.Exists(transcript)) length = new FileInfo(transcript).Length; }
    catch (IOException) { }

    // Claude Code tells its child processes where it runs: "cli" in a terminal, something else in the Desktop app.
    string? origin = Environment.GetEnvironmentVariable("CLAUDE_CODE_ENTRYPOINT");

    SessionStore.Update(sessions =>
    {
        if (!sessions.TryGetValue(id, out var info)) sessions[id] = info = new SessionInfo();
        info.Cwd = Text("cwd") ?? info.Cwd;
        if (!string.IsNullOrEmpty(origin)) info.Origin = origin;
        info.State = state;
        info.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        info.Transcript = transcript ?? info.Transcript;
        info.TranscriptLength = length;
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
    if (s.SevenDay != null) parts.Add(FormatWindow("Woche", s.SevenDay, now, showReset: false));
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
