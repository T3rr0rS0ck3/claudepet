using System.IO;
using System.Windows.Controls;
using ClaudePet.Core;

namespace ClaudePet.Views;

/// <summary>Popup menu listing recent projects and all project folders inside the repo folder.</summary>
public static class ProjectMenu
{
    public static ContextMenu Build(AppSettings settings, Action<string> open, Action chooseOther, Action chooseRepos)
    {
        var menu = new ContextMenu();
        void Add(string header, Action action, string? tooltip = null)
        {
            var item = new MenuItem { Header = header, ToolTip = tooltip };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }
        void Heading(string text) =>
            menu.Items.Add(new MenuItem { Header = text, IsEnabled = false, FontWeight = System.Windows.FontWeights.SemiBold });

        var recent = settings.RecentProjects.Where(Directory.Exists).ToList();
        if (recent.Count > 0)
        {
            Heading("Zuletzt geöffnet");
            foreach (string folder in recent) Add(Escape(ClaudeLauncher.FolderName(folder)), () => open(folder), folder);
        }

        var projects = ProjectFolders(settings.ReposPath);
        if (projects.Count > 0)
        {
            if (menu.Items.Count > 0) menu.Items.Add(new Separator());
            Heading(ClaudeLauncher.FolderName(settings.ReposPath!));
            foreach (string folder in projects) Add(Escape(Path.GetFileName(folder)), () => open(folder), folder);
        }

        if (menu.Items.Count > 0) menu.Items.Add(new Separator());
        Add("Anderen Ordner wählen…", chooseOther);
        Add("Repo-Ordner festlegen…", chooseRepos, settings.ReposPath);
        return menu;
    }

    private static List<string> ProjectFolders(string? reposPath)
    {
        if (reposPath == null || !Directory.Exists(reposPath)) return [];
        try
        {
            return new DirectoryInfo(reposPath).EnumerateDirectories()
                .Where(d => !d.Name.StartsWith('.') && !d.Attributes.HasFlag(FileAttributes.Hidden)
                            && !d.Attributes.HasFlag(FileAttributes.System))
                .Select(d => d.FullName)
                .OrderBy(path => Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Write("Repo-Ordner konnte nicht gelesen werden: " + ex.Message);
            return [];
        }
    }

    // A single underscore in a menu header marks the access key; double it to show it literally.
    private static string Escape(string name) => name.Replace("_", "__");
}
