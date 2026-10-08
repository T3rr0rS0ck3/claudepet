using System.Runtime.InteropServices;

namespace ClaudePet.Core;

/// <summary>Whether the app runs as an MSIX package (the Microsoft Store version).</summary>
public static class AppPackage
{
    private const int APPMODEL_ERROR_NO_PACKAGE = 15700;

    /// <summary>
    /// True for the Store version. It updates through the Store, starts with Windows through a startup
    /// task and reaches the bridge through an app execution alias instead of its versioned install folder.
    /// </summary>
    public static bool IsPackaged { get; } = DetectPackage();

    private static bool DetectPackage()
    {
        int length = 0;
        return GetCurrentPackageFullName(ref length, null) != APPMODEL_ERROR_NO_PACKAGE;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int length, char[]? fullName);
}
