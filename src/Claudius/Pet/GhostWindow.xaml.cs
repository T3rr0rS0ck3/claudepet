using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Claudius.Pet;

/// <summary>
/// Translucent copy of the pet that follows the cursor during a right-button drag.
/// Click-through, so the window below it can be found under the cursor.
/// </summary>
public partial class GhostWindow : Window
{
    private const double IdleOpacity = 0.55;
    private const double TargetOpacity = 0.9;

    private readonly DispatcherTimer _timer = new()
    {
        Interval = TimeSpan.FromMilliseconds(1000.0 / PetAnimator.TicksPerSecond),
    };
    private long _tick;
    private bool _highlight;

    public GhostWindow(double scale)
    {
        InitializeComponent();
        GhostImage.Width = Sprite.Width * scale;
        GhostImage.Height = Sprite.Height * scale;
        Opacity = IdleOpacity;
        _timer.Tick += (_, _) => { _tick++; Render(); };
        SourceInitialized += (_, _) =>
        {
            Handle = new WindowInteropHelper(this).Handle;
            int style = GetWindowLong(Handle, GWL_EXSTYLE);
            SetWindowLong(Handle, GWL_EXSTYLE, style | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        };
        Closed += (_, _) => _timer.Stop();
        Render();
        _timer.Start();
    }

    public IntPtr Handle { get; private set; }

    /// <summary>Centers the ghost on a point in physical screen pixels.</summary>
    public void MoveTo(int x, int y)
    {
        if (Handle == IntPtr.Zero || !GetWindowRect(Handle, out var r)) return;
        int w = r.Right - r.Left, h = r.Bottom - r.Top;
        SetWindowPos(Handle, HWND_TOPMOST, x - w / 2, y - h / 2, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
    }

    /// <summary>Brighter and arms up while over a place where the assistant can be opened.</summary>
    public void SetHighlight(bool highlight)
    {
        if (highlight == _highlight) return;
        _highlight = highlight;
        Opacity = highlight ? TargetOpacity : IdleOpacity;
        Render();
    }

    /// <summary>Floats up while fading out, then closes.</summary>
    public void Vanish()
    {
        var duration = TimeSpan.FromMilliseconds(_highlight ? 700 : 250);
        var fade = new DoubleAnimation(0, duration);
        fade.Completed += (_, _) => Close();
        if (_highlight)
            BeginAnimation(TopProperty, new DoubleAnimation(Top - 60, duration) { EasingFunction = new QuadraticEase() });
        BeginAnimation(OpacityProperty, fade);
    }

    private void Render()
    {
        var frame = _highlight
            ? new SpriteFrame(Eyes.Happy, Mouth.Smile, _tick / 2 % 2 == 0 ? Arms.Up : Arms.Wave, Bob: (int)(_tick / 2 % 2))
            : new SpriteFrame(_tick % 30 == 0 ? Eyes.Blink : Eyes.Normal, Mouth.Small, Bob: (int)(_tick / 4 % 2));
        GhostImage.Source = Sprite.Render(frame with { Tint = Tint.Ghost });
    }

    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
}
