using ClaudePet.Core;
using ClaudePet.Pet;
using Forms = System.Windows.Forms;

namespace ClaudePet;

/// <summary>Notification-area icon (WinForms, since WPF has none).</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly App _app;
    private readonly Forms.NotifyIcon _icon = new();
    private readonly Dictionary<PetMood, System.Drawing.Icon> _icons = new();
    private PetMood? _mood;
    private int _spriteVersion = Sprite.Version;

    public TrayIcon(App app)
    {
        _app = app;
        BuildMenu();
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) app.ShowUsage();
        };
        _icon.Text = "Claudius";
        Update(PetMood.Unknown, Strings.TrayWaiting);
        _icon.Visible = true;
    }

    /// <summary>Builds the right-click menu, again after the language changed.</summary>
    public void BuildMenu()
    {
        var app = _app;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(Strings.ShowUsage, null, (_, _) => app.ShowUsage());
        menu.Items.Add(Strings.OpenClaude, null, (_, _) => app.ShowProjectMenu());
        var voice = new Forms.ToolStripMenuItem(Strings.StartVoiceChat, null, (_, _) => app.StartVoiceChat());
        menu.Items.Add(voice);
        var petVisible = new Forms.ToolStripMenuItem(Strings.ShowPet, null, (_, _) => app.TogglePetVisible());
        var alwaysOnTop = new Forms.ToolStripMenuItem(Strings.AlwaysOnTop, null, (_, _) => app.ToggleAlwaysOnTop());
        var walkAround = new Forms.ToolStripMenuItem(Strings.WalkAround, null, (_, _) => app.ToggleWalkAround());
        menu.Items.Add(petVisible);
        menu.Items.Add(alwaysOnTop);
        menu.Items.Add(walkAround);
        menu.Items.Add(new Forms.ToolStripSeparator());
        var update = new Forms.ToolStripMenuItem("", null, (_, _) => app.InstallUpdate());
        menu.Items.Add(update);
        menu.Items.Add(Strings.ConnectMenu, null, (_, _) => app.ConnectClaudeCode(null));
        menu.Items.Add(Strings.SettingsMenu, null, (_, _) => app.ShowSettings());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(Strings.Quit, null, (_, _) => app.Quit());
        menu.Opening += (_, _) =>
        {
            petVisible.Checked = app.PetVisible;
            alwaysOnTop.Checked = app.Settings.AlwaysOnTop;
            walkAround.Checked = app.Settings.WalkAround;
            voice.Visible = app.Settings.VoiceChat;
            update.Text = app.UpdateMenuText;
            update.Visible = app.AvailableUpdate != null;
        };

        var old = _icon.ContextMenuStrip;
        _icon.ContextMenuStrip = menu;
        old?.Dispose();
    }

    public void Update(PetMood mood, string tooltip)
    {
        if (_spriteVersion != Sprite.Version)
        {
            // The pet's color changed: redraw the icons.
            _spriteVersion = Sprite.Version;
            var old = _icons.Values.ToList();
            _icons.Clear();
            _mood = null;
            Update(mood, tooltip);
            foreach (var icon in old) icon.Dispose();
            return;
        }
        if (_mood != mood)
        {
            _mood = mood;
            if (!_icons.TryGetValue(mood, out var icon))
            {
                // A calm, readable frame of the current mood
                var frame = PetAnimator.Frame(mood, working: false, cheering: false, t: 1) with { Mark = Mark.None, Shake = 0, Bob = 0 };
                icon = _icons[mood] = Sprite.CreateIcon(frame);
            }
            _icon.Icon = icon;
        }
        _icon.Text = tooltip.Length > 127 ? tooltip[..127] : tooltip;
    }

    public void ShowBalloon(string title, string text) =>
        _icon.ShowBalloonTip(6000, title, text, Forms.ToolTipIcon.Warning);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        foreach (var icon in _icons.Values) icon.Dispose();
    }
}
