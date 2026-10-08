using ClaudePet.Core;
using ClaudePet.Pet;
using Forms = System.Windows.Forms;

namespace ClaudePet;

/// <summary>Notification-area icon (WinForms, since WPF has none).</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon = new();
    private readonly Forms.ToolStripMenuItem _petVisible;
    private readonly Forms.ToolStripMenuItem _alwaysOnTop;
    private readonly Dictionary<PetMood, System.Drawing.Icon> _icons = new();
    private PetMood? _mood;

    public TrayIcon(App app)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Usage anzeigen", null, (_, _) => app.ShowUsage());
        menu.Items.Add("Claude öffnen…", null, (_, _) => app.ShowProjectMenu());
        var voice = new Forms.ToolStripMenuItem("Sprachchat starten…", null, (_, _) => app.StartVoiceChat());
        menu.Items.Add(voice);
        _petVisible = new Forms.ToolStripMenuItem("Pet anzeigen", null, (_, _) => app.TogglePetVisible());
        _alwaysOnTop = new Forms.ToolStripMenuItem("Immer im Vordergrund", null, (_, _) => app.ToggleAlwaysOnTop());
        menu.Items.Add(_petVisible);
        menu.Items.Add(_alwaysOnTop);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Claude Code verbinden…", null, (_, _) => app.ConnectClaudeCode(null));
        menu.Items.Add("Einstellungen…", null, (_, _) => app.ShowSettings());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Beenden", null, (_, _) => app.Quit());
        menu.Opening += (_, _) =>
        {
            _petVisible.Checked = app.PetVisible;
            _alwaysOnTop.Checked = app.Settings.AlwaysOnTop;
            voice.Visible = app.Settings.VoiceChat;
        };

        _icon.ContextMenuStrip = menu;
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) app.ShowUsage();
        };
        _icon.Text = "Claude Pet";
        Update(PetMood.Unknown, "Claude Pet – warte auf Daten");
        _icon.Visible = true;
    }

    public void Update(PetMood mood, string tooltip)
    {
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
