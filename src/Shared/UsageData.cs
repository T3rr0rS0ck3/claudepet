using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace ClaudePet.Shared;

/// <summary>One rate-limit window as delivered by Claude Code's statusLine JSON.</summary>
public sealed class RateWindow
{
    [JsonPropertyName("used_percentage")] public double UsedPercentage { get; set; }

    /// <summary>Unix epoch seconds.</summary>
    [JsonPropertyName("resets_at")] public long ResetsAt { get; set; }

    [JsonIgnore] public DateTimeOffset ResetsAtTime => DateTimeOffset.FromUnixTimeSeconds(ResetsAt);
}

/// <summary>Latest known usage state, written by the bridge and read by the desktop app.</summary>
public sealed class UsageSnapshot
{
    /// <summary>Unix epoch seconds of the last statusLine call that carried rate-limit data.</summary>
    [JsonPropertyName("updated_at")] public long UpdatedAt { get; set; }
    [JsonPropertyName("five_hour")] public RateWindow? FiveHour { get; set; }
    [JsonPropertyName("seven_day")] public RateWindow? SevenDay { get; set; }
    [JsonPropertyName("model")] public string? Model { get; set; }

    [JsonIgnore] public DateTimeOffset UpdatedAtTime => DateTimeOffset.FromUnixTimeSeconds(UpdatedAt);
}

/// <summary>One line of history.jsonl, used for the forecast.</summary>
public sealed class HistorySample
{
    [JsonPropertyName("t")] public long Time { get; set; }
    [JsonPropertyName("s")] public double? Session { get; set; }
    [JsonPropertyName("sr")] public long? SessionReset { get; set; }
    [JsonPropertyName("w")] public double? Week { get; set; }
    [JsonPropertyName("wr")] public long? WeekReset { get; set; }
}

public static class DataPaths
{
    /// <summary>%LOCALAPPDATA%\ClaudePet, overridable via CLAUDEPET_DATA_DIR (useful for testing).</summary>
    public static string DataDir { get; } =
        Environment.GetEnvironmentVariable("CLAUDEPET_DATA_DIR") is { Length: > 0 } custom
            ? custom
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudePet");

    public static string UsageFile => Path.Combine(DataDir, "usage.json");
    public static string HistoryFile => Path.Combine(DataDir, "history.jsonl");
    public static string SettingsFile => Path.Combine(DataDir, "settings.json");
    public static string LogFile => Path.Combine(DataDir, "log.txt");
}

public static class UsageStore
{
    private const long MaxHistoryBytes = 512 * 1024;
    private const int KeepHistoryLines = 2000;

    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static UsageSnapshot? TryRead(string path)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (!File.Exists(path)) return null;
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                return JsonSerializer.Deserialize<UsageSnapshot>(stream, Options);
            }
            catch (IOException) { Thread.Sleep(15); }
            catch (UnauthorizedAccessException) { Thread.Sleep(15); }
            catch (JsonException) { return null; }
        }
        return null;
    }

    /// <summary>Writes to a temp file and swaps it in so readers never see a half-written file.</summary>
    public static void WriteAtomic(string path, UsageSnapshot snapshot)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + "." + Environment.ProcessId + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(snapshot, Options), new UTF8Encoding(false));
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(temp, path, overwrite: true);
                return;
            }
            catch (Exception ex) when (attempt < 5 && ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(20);
            }
        }
    }

    public static void AppendHistory(HistorySample sample)
    {
        string path = DataPaths.HistoryFile;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.AppendAllText(path, JsonSerializer.Serialize(sample, Options) + "\n", new UTF8Encoding(false));

        var info = new FileInfo(path);
        if (info.Length > MaxHistoryBytes)
        {
            var lines = File.ReadAllLines(path);
            int skip = Math.Max(0, lines.Length - KeepHistoryLines);
            File.WriteAllLines(path, lines[skip..], new UTF8Encoding(false));
        }
    }

    public static List<HistorySample> ReadHistory()
    {
        var result = new List<HistorySample>();
        try
        {
            if (!File.Exists(DataPaths.HistoryFile)) return result;
            using var stream = new FileStream(DataPaths.HistoryFile, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var sample = JsonSerializer.Deserialize<HistorySample>(line, Options);
                    if (sample != null) result.Add(sample);
                }
                catch (JsonException) { }
            }
        }
        catch (IOException) { }
        return result;
    }
}
