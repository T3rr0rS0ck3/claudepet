using Microsoft.Win32;

namespace ClaudePet.Core;

public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ClaudePet";

    private static string Command => $"\"{Environment.ProcessPath}\"";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string value && value == Command;
    }

    public static void Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled) key.SetValue(ValueName, Command);
            else if (key.GetValue(ValueName) != null) key.DeleteValue(ValueName);
        }
        catch (Exception ex)
        {
            Log.Write("Autostart konnte nicht gesetzt werden: " + ex.Message);
        }
    }
}
