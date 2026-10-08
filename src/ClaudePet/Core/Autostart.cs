using Microsoft.Win32;
using Windows.ApplicationModel;

namespace ClaudePet.Core;

/// <summary>
/// Start with Windows: a Run entry in the registry, or for the Store version the package's startup task
/// (declared in packaging/AppxManifest.xml), which also shows up under Settings → Apps → Startup.
/// </summary>
public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ClaudePet";
    private const string TaskId = "ClaudePetStartup";

    private static string Command => $"\"{Environment.ProcessPath}\"";

    public static bool IsEnabled()
    {
        if (AppPackage.IsPackaged) return TaskState() is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string value && value == Command;
    }

    /// <summary>
    /// The user switched the Store version's startup off in the Windows settings; only they can switch
    /// it on again there.
    /// </summary>
    public static bool BlockedByUser => AppPackage.IsPackaged && TaskState() == StartupTaskState.DisabledByUser;

    public static void Set(bool enabled)
    {
        try
        {
            if (AppPackage.IsPackaged)
            {
                var task = StartupTask.GetAsync(TaskId).AsTask().GetAwaiter().GetResult();
                // Packaged desktop apps get no consent prompt here; DisabledByUser stays disabled.
                if (enabled) task.RequestEnableAsync().AsTask().GetAwaiter().GetResult();
                else task.Disable();
                return;
            }
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled) key.SetValue(ValueName, Command);
            else if (key.GetValue(ValueName) != null) key.DeleteValue(ValueName);
        }
        catch (Exception ex)
        {
            Log.Write("Autostart konnte nicht gesetzt werden: " + ex.Message);
        }
    }

    private static StartupTaskState? TaskState()
    {
        try
        {
            return StartupTask.GetAsync(TaskId).AsTask().GetAwaiter().GetResult().State;
        }
        catch (Exception ex)
        {
            Log.Write("Autostart-Status nicht lesbar: " + ex.Message);
            return null;
        }
    }
}
