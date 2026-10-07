using System.IO;
using ClaudePet.Shared;

namespace ClaudePet.Core;

public static class Log
{
    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(DataPaths.DataDir);
            File.AppendAllText(DataPaths.LogFile, $"{DateTime.Now:O} [app] {message}\n");
        }
        catch { }
    }
}
