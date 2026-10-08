using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using ClaudePet.Core;
using ClaudePet.Shared;

namespace ClaudePet.Pet;

/// <summary>
/// A small pet for one Claude Code session. It trots after the big pet and shows that session's
/// "?" while a question waits and wears the outfit for that session's model; the tooltip names the folder
/// and its state.
/// </summary>
public sealed class BabyPetWindow : Window
{
    public const double SizeFactor = 0.5;

    private readonly Image _image = new() { Stretch = Stretch.Fill };
    private SessionView _session;
    private Outfit _outfit;
    private Motion _motion = Motion.Idle;
    private int _direction = 1;

    /// <summary>Feet position in physical pixels; NaN until placed.</summary>
    public double X { get; private set; } = double.NaN;
    public double Y { get; private set; } = double.NaN;

    public BabyPetWindow(SessionView session, Outfit outfit, double petScale, bool topmost)
    {
        _session = session;
        _outfit = outfit;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        Topmost = topmost;
        Title = "Claudius – " + session.Folder;
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
        Content = _image;
        SetScale(petScale);
        UpdateToolTip();
        Left = -10000;
        Top = -10000;
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int style = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, (style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE) & ~WS_EX_APPWINDOW);
        };
    }

    public void SetScale(double petScale)
    {
        Width = _image.Width = Sprite.Width * petScale * SizeFactor;
        Height = _image.Height = Sprite.Height * petScale * SizeFactor;
    }

    public void SetSession(SessionView session, Outfit outfit)
    {
        _session = session;
        _outfit = outfit;
        Title = "Claudius – " + session.Folder;
        UpdateToolTip();
    }

    /// <summary>Moves a step towards the target (feet, physical pixels).</summary>
    public void Follow(double targetX, double targetY, double dt)
    {
        if (double.IsNaN(X))
        {
            X = targetX;
            Y = targetY;
        }
        else
        {
            double k = Math.Min(1, dt * 5);
            double dx = (targetX - X) * k;
            X += dx;
            Y += (targetY - Y) * k;
            if (Math.Abs(dx) > 0.3) _direction = dx < 0 ? -1 : 1;
            _motion = Math.Abs(targetX - X) > 3 ? Motion.Walk : Motion.Idle;
        }

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var rect)) return;
        int x = (int)Math.Round(X - (rect.Right - rect.Left) / 2.0), y = (int)Math.Round(Y - (rect.Bottom - rect.Top));
        if (x != rect.Left || y != rect.Top)
            SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    public void Render(PetMood mood, long t)
    {
        var mark = _session.State == SessionStates.Question ? Mark.Question : Mark.None;
        bool working = _session.State == SessionStates.Working;
        var frame = PetAnimator.Frame(mood, working && _motion == Motion.Idle, false, t, _motion, _direction);
        _image.Source = Sprite.Render(frame with { Mark = mark, Outfit = _outfit });
    }

    private void UpdateToolTip() => ToolTip = Strings.SessionTip(_session.Folder, _session.State);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
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
