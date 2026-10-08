using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using ClaudePet.Core;
using ClaudePet.Pet;
using Forms = System.Windows.Forms;

namespace ClaudePet;

/// <summary>
/// Notification-area icon (WinForms, since WPF has none). Its right-click menu is a WPF menu in the look of the
/// pet's own menu.
/// </summary>
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
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) app.ShowUsage();
        };
        _icon.MouseUp += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Right) ShowMenu();
        };
        _icon.Text = "Claudius";
        Update(PetMood.Unknown, Strings.TrayWaiting);
        _icon.Visible = true;
    }

    /// <summary>Opens the menu at the cursor; built anew each time, so it shows the current state and language.</summary>
    private void ShowMenu()
    {
        var app = _app;
        var menu = new ContextMenu
        {
            Style = (Style)Application.Current.FindResource("PetMenu"),
            Placement = PlacementMode.MousePoint,
        };
        void Item(string header, Action action, bool isChecked = false)
        {
            var item = new MenuItem { Header = header, IsChecked = isChecked };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }

        Item(Strings.ShowUsage, app.ShowUsage);
        Item(Strings.OpenClaude, app.ShowProjectMenu);
        if (app.Settings.VoiceChat) Item(Strings.StartVoiceChat, app.StartVoiceChat);
        Item(Strings.ShowPet, app.TogglePetVisible, app.PetVisible);
        Item(Strings.AlwaysOnTop, app.ToggleAlwaysOnTop, app.Settings.AlwaysOnTop);
        Item(Strings.WalkAround, app.ToggleWalkAround, app.Settings.WalkAround);
        menu.Items.Add(new Separator());
        if (app.AvailableUpdate != null) Item(app.UpdateMenuText, () => app.InstallUpdate());
        Item(Strings.ConnectMenu, () => app.ConnectClaudeCode(null));
        Item(Strings.SettingsMenu, app.ShowSettings);
        menu.Items.Add(new Separator());
        Item(Strings.Quit, app.Quit);

        // Without the focus the menu would stay open when clicking elsewhere, e.g. on the desktop.
        menu.Opened += (_, _) =>
        {
            if (PresentationSource.FromVisual(menu) is HwndSource source) SetForegroundWindow(source.Handle);
        };
        menu.IsOpen = true;
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

    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
}
