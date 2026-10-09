using System.IO;
using System.Text.Json.Nodes;
using Microsoft.Win32;
using Claudius.Shared;

namespace Claudius.Core;

/// <summary>
/// Takes over what versions before the rename to Claudius left behind: their data folder, autostart
/// entry, bridge registration, terminal theme and settings names. The old names live here and nowhere else.
/// </summary>
public static class Legacy
{
    private const string OldName = "ClaudePet";
    /// <summary>The old bridge, still registered as statusLine and in the hooks until <see cref="AssistantSetup.Repoint"/>.</summary>
    public const string OldBridgeName = OldName + "Bridge";
    private const string OldThemeValue = "custom:claudepet";

    private static readonly (string Old, string New)[] RenamedSettings =
    [
        ("ClaudeMascotColor", nameof(AppSettings.TerminalMascotColor)),
        ("ClaudeThemeSwitched", nameof(AppSettings.MascotThemeSwitched)),
        ("ClaudeThemeBefore", nameof(AppSettings.MascotThemeBefore)),
    ];

    private static string OldDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), OldName);

    /// <summary>
    /// Moves the old data folder's files into the new one, keeping files the new one already has, and
    /// removes it. A custom data folder (<c>CLAUDIUS_DATA_DIR</c>) is left alone.
    /// </summary>
    public static void MigrateDataDir()
    {
        string old = OldDataDir;
        if (!Directory.Exists(old) || string.Equals(Path.GetFullPath(old), Path.GetFullPath(DataPaths.DataDir),
                StringComparison.OrdinalIgnoreCase)) return;
        if (Environment.GetEnvironmentVariable("CLAUDIUS_DATA_DIR") is { Length: > 0 }) return;
        try
        {
            foreach (string file in Directory.EnumerateFiles(old, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(DataPaths.DataDir, Path.GetRelativePath(old, file));
                if (File.Exists(target)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
            Directory.Delete(old, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An old bridge may still be writing there; what was copied is enough, the rest follows next start.
            Log.Write("Alter Datenordner konnte nicht vollständig übernommen werden: " + ex.Message);
        }
    }

    /// <summary>Settings saved under their old names get the new ones.</summary>
    public static void RenameSettings(JsonObject settings)
    {
        foreach (var (oldKey, newKey) in RenamedSettings)
        {
            if (!settings.ContainsKey(oldKey)) continue;
            var value = settings[oldKey];
            settings.Remove(oldKey);
            if (!settings.ContainsKey(newKey)) settings[newKey] = value;
        }
    }

    /// <summary>Removes the old autostart entry; the new one is set from the settings as usual.</summary>
    public static void RemoveOldAutostart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key?.GetValue(OldName) != null) key.DeleteValue(OldName);
        }
        catch (Exception ex)
        {
            Log.Write("Alter Autostart-Eintrag konnte nicht entfernt werden: " + ex.Message);
        }
    }

    /// <summary>
    /// The old terminal theme is still selected: select the new one instead (or the theme from before, if
    /// the mascot color is off by now) and delete the old file.
    /// </summary>
    public static void MigrateTheme(AppSettings settings)
    {
        try
        {
            if (AssistantSetup.CurrentTheme() == OldThemeValue)
            {
                if (settings.TerminalMascotColor)
                {
                    AssistantSetup.WritePetTheme(settings.PetColor, settings.MascotThemeBefore);
                    AssistantSetup.SelectPetTheme();
                }
                else
                {
                    AssistantSetup.SelectTheme(settings.MascotThemeBefore);
                }
            }
            string oldFile = Path.Combine(AssistantSetup.ThemesDir, OldThemeValue["custom:".Length..] + ".json");
            if (File.Exists(oldFile)) File.Delete(oldFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
                                       or System.Text.Json.JsonException)
        {
            Log.Write("Altes Theme konnte nicht übernommen werden: " + ex.Message);
        }
    }
}
