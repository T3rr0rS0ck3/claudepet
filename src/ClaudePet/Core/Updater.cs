using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace ClaudePet.Core;

/// <summary>A newer release on GitHub.</summary>
public sealed record UpdateInfo(Version Version, string SetupUrl, string? ChecksumUrl, string PageUrl)
{
    public string VersionText => Updater.Format(Version);
}

/// <summary>
/// Looks for newer releases on GitHub and installs them: downloads the setup, checks it against the
/// release's SHA256SUMS.txt and runs it silently, which closes the app and starts the new version.
/// </summary>
public static class Updater
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/T3rr0rS0ck3/claudepet/releases/latest";
    public const string ReleasesPage = "https://github.com/T3rr0rS0ck3/claudepet/releases/latest";

    /// <summary>The running version; 0.0.0 for self-built copies.</summary>
    public static Version CurrentVersion { get; } = ReadVersion();

    // After CurrentVersion: static fields initialize in order and the client's User-Agent uses it.
    private static readonly HttpClient Http = CreateClient();

    /// <summary>Self-built copies (version 0.0.0) never look for updates.</summary>
    public static bool IsDevBuild => CurrentVersion == new Version(0, 0, 0);

    /// <summary>Installed by the setup (which can update it), not a portable copy.</summary>
    public static bool IsInstalled => File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"));

    public static string Format(Version v) => $"{v.Major}.{v.Minor}.{v.Build}";

    /// <summary>The latest release if it is newer than the running version, otherwise null. Throws on network errors.</summary>
    public static async Task<UpdateInfo?> CheckAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await Http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;

        // "latest" never returns pre-releases; tags look like v1.2.3.
        if (!TryParse(root.GetProperty("tag_name").GetString(), out var latest) || latest <= CurrentVersion) return null;

        string setupName = $"ClaudePet-Setup-{Format(latest)}.exe";
        string? setupUrl = null, checksumUrl = null;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            string? name = asset.GetProperty("name").GetString();
            string? url = asset.GetProperty("browser_download_url").GetString();
            if (name == setupName) setupUrl = url;
            else if (name == "SHA256SUMS.txt") checksumUrl = url;
        }
        string page = root.GetProperty("html_url").GetString() ?? ReleasesPage;
        return setupUrl == null ? null : new UpdateInfo(latest, setupUrl, checksumUrl, page);
    }

    /// <summary>
    /// Downloads and verifies the setup and starts it silently. The caller must quit right after, so the
    /// setup can replace the files; the setup starts the new version when it is done.
    /// </summary>
    public static async Task InstallAsync(UpdateInfo update)
    {
        if (update.ChecksumUrl == null) throw new InvalidOperationException("Das Release enthält keine SHA256SUMS.txt.");

        string dir = Path.Combine(Path.GetTempPath(), "ClaudePet-Update");
        Directory.CreateDirectory(dir);
        string fileName = Path.GetFileName(new Uri(update.SetupUrl).LocalPath);
        string setup = Path.Combine(dir, fileName);

        string sums = await Http.GetStringAsync(update.ChecksumUrl);
        string expected = ExpectedHash(sums, fileName)
                          ?? throw new InvalidOperationException($"Keine Prüfsumme für {fileName} gefunden.");

        await using (var download = await Http.GetStreamAsync(update.SetupUrl))
        await using (var file = File.Create(setup))
            await download.CopyToAsync(file);

        string actual;
        await using (var file = File.OpenRead(setup))
            actual = Convert.ToHexString(await SHA256.HashDataAsync(file));
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(setup);
            throw new InvalidOperationException("Die Prüfsumme des Downloads stimmt nicht – Update abgebrochen.");
        }

        Process.Start(new ProcessStartInfo(setup, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /RESTARTAPP=1")
        {
            UseShellExecute = true,
        });
    }

    /// <summary>The hash for a file from "hash  name" lines (sha256sum format).</summary>
    private static string? ExpectedHash(string sums, string fileName) =>
        sums.Split('\n')
            .Select(line => line.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length == 2 && parts[1].Trim().TrimStart('*') == fileName)
            .Select(parts => parts[0])
            .FirstOrDefault();

    private static bool TryParse(string? text, out Version version)
    {
        version = new Version(0, 0, 0);
        string core = (text ?? "").Trim().TrimStart('v', 'V').Split('-', '+')[0];
        if (!Version.TryParse(core, out var parsed)) return false;
        version = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0));
        return true;
    }

    private static Version ReadVersion()
    {
        string? info = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return TryParse(info, out var version) ? version : new Version(0, 0, 0);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"ClaudePet/{Format(CurrentVersion)}");
        return client;
    }
}
