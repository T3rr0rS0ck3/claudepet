using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Claudius.Pet;

/// <summary>
/// The grim reaper that turns up when a session's context window is nearly full: it fades in behind the pet,
/// floats after it, and fades away rising once /compact or /clear made the pet well again. Clicks go through it.
/// </summary>
public sealed class ReaperWindow : Window
{
    /// <summary>How far it floats above the ground, in pet pixels.</summary>
    private const double Hover = 1.5;
    /// <summary>Seconds of each step of the smoke cloud it vanishes and reappears in.</summary>
    private const double PoofStep = 0.1;
    /// <summary>It changes sides at most this often (seconds), so a pet turning back and forth does not keep it puffing.</summary>
    private const double PoofPause = 0.8;

    private readonly Image _image = new() { Stretch = Stretch.Fill };
    private double _scale;
    private bool _leaving;
    /// <summary>The side of the pet it floats on: -1 left, 1 right; behind the pet, so it looks the way the pet walks.</summary>
    private int _side;
    private int _phase;
    /// <summary>Changing sides: since when, and whether it already moved over.</summary>
    private DateTime? _poofStart;
    private bool _movedOver;
    private DateTime _lastPoof;

    /// <summary>Feet position in physical pixels; NaN until placed.</summary>
    public double X { get; private set; } = double.NaN;
    public double Y { get; private set; } = double.NaN;

    public ReaperWindow(double petScale, bool topmost)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        IsHitTestVisible = false;
        Topmost = topmost;
        Title = "Claudius";
        Opacity = 0;
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
        Content = _image;
        SetScale(petScale);
        Left = -10000;
        Top = -10000;
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int style = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE,
                (style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT | WS_EX_LAYERED) & ~WS_EX_APPWINDOW);
        };
        Loaded += (_, _) => BeginAnimation(OpacityProperty, new DoubleAnimation(0, 0.92, TimeSpan.FromSeconds(1.5)));
    }

    public void SetScale(double petScale)
    {
        _scale = petScale;
        Width = _image.Width = Sprite.Width * petScale;
        Height = _image.Height = Sprite.Height * petScale;
    }

    /// <summary>
    /// Floats a step towards the target behind the pet (feet, physical pixels); <paramref name="side"/> says which
    /// side of the pet that is. When the pet turns round it does not drift through it: it vanishes in a puff of
    /// smoke and reappears behind the pet again, looking the new way.
    /// </summary>
    public void Follow(double targetX, double targetY, int side, double dt)
    {
        if (_leaving) return;
        var now = DateTime.Now;
        if (double.IsNaN(X))
        {
            X = targetX;
            Y = targetY;
            _side = side;
        }
        else if (side != _side && _poofStart == null && (now - _lastPoof).TotalSeconds >= PoofPause)
        {
            _poofStart = now;
            _movedOver = false;
        }

        if (_poofStart is { } start)
        {
            double elapsed = (now - start).TotalSeconds;
            if (elapsed >= Sprite.PoofSteps * PoofStep && !_movedOver)
            {
                // Gone in the smoke: over to the pet's other side
                X = targetX;
                Y = targetY;
                _side = side;
                _movedOver = true;
            }
            if (elapsed >= 2 * Sprite.PoofSteps * PoofStep)
            {
                _poofStart = null;
                _lastPoof = now;
            }
        }
        else
        {
            // It drifts after the pet rather than walking
            double k = Math.Min(1, dt * 2.5);
            X += (targetX - X) * k;
            Y += (targetY - Y) * k;
        }
        Move();
        Draw();
    }

    public void Render(long t)
    {
        _phase = (int)(t / 4 % 4);
        Draw();
    }

    /// <summary>The reaper looking the way the pet walks, wrapped in smoke while it changes sides: thicker and thicker, then thinning out.</summary>
    private void Draw()
    {
        int poof = 0;
        if (_poofStart is { } start)
        {
            int step = (int)((DateTime.Now - start).TotalSeconds / PoofStep);
            poof = step < Sprite.PoofSteps ? step + 1 : Math.Max(0, 2 * Sprite.PoofSteps - step);
        }
        _image.Source = Sprite.RenderReaper(_phase, _side > 0, poof);
    }

    /// <summary>Fades away while rising, then closes.</summary>
    public void Leave()
    {
        if (_leaving) return;
        _leaving = true;
        var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromSeconds(1.5));
        fade.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fade);
        var rise = new DoubleAnimation(0, -Height * 0.6, TimeSpan.FromSeconds(1.5));
        var shift = new TranslateTransform();
        _image.RenderTransform = shift;
        shift.BeginAnimation(TranslateTransform.YProperty, rise);
    }

    private void Move()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var rect)) return;
        double hover = Hover * _scale * (rect.Bottom - rect.Top) / Math.Max(1, Height);
        int x = (int)Math.Round(X - (rect.Right - rect.Left) / 2.0), y = (int)Math.Round(Y - hover - (rect.Bottom - rect.Top));
        if (x != rect.Left || y != rect.Top)
            SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_APPWINDOW = 0x00040000;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
}
