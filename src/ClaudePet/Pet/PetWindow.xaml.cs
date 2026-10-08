using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ClaudePet.Core;

namespace ClaudePet.Pet;

public partial class PetWindow : Window
{
    private const double BubbleArea = 130;
    private const double MinWidth_ = 260;

    private readonly DispatcherTimer _animationTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(1000.0 / PetAnimator.TicksPerSecond),
    };
    private readonly DispatcherTimer _bubbleTimer = new();
    // A single click waits for the double-click time, so a double-click does not also flash the overlay.
    private readonly DispatcherTimer _clickTimer = new() { Interval = TimeSpan.FromMilliseconds(GetDoubleClickTime()) };
    private DateTime _clickedAt;

    // Right-button drag: a ghost copy of the pet is carried to an Explorer window.
    private readonly DispatcherTimer _ghostTimer = new() { Interval = TimeSpan.FromMilliseconds(15) };
    private GhostWindow? _ghost;
    private Point? _rightPressPoint;
    private bool _suppressContextMenu;
    private bool _ghostDrag = true;
    private double _scale = 5;

    private long _tick = 1;
    private PetMood _mood = PetMood.Unknown;
    private bool _working;
    private bool _animations = true;
    private DateTime _cheerUntil;
    private Point? _pressPoint;
    private Eyes? _lookAt;

    /// <summary>Left click without dragging; carries the time the button was released.</summary>
    public event Action<DateTime>? Clicked;
    /// <summary>Left double-click.</summary>
    public event Action? DoubleClicked;
    /// <summary>
    /// The ghost was dropped: the folder under it (null if none) and whether it was over Explorer
    /// or the desktop at all.
    /// </summary>
    public event Action<string?, bool>? GhostDropped;
    /// <summary>The pet was dragged to a new position.</summary>
    public event Action? Moved;

    public PetWindow()
    {
        InitializeComponent();
        _animationTimer.Tick += (_, _) => { _tick++; Render(); };
        _bubbleTimer.Tick += (_, _) => HideBubble();
        _ghostTimer.Tick += (_, _) => UpdateGhost();
        _clickTimer.Tick += (_, _) =>
        {
            _clickTimer.Stop();
            Clicked?.Invoke(_clickedAt);
        };
        SourceInitialized += (_, _) => HideFromAltTab();
    }

    public void ApplySettings(AppSettings settings, bool initial = false)
    {
        Topmost = settings.AlwaysOnTop;
        _animations = settings.Animations;
        _ghostDrag = settings.GhostDrag;
        _scale = settings.PetScale;

        double petWidth = Sprite.Width * settings.PetScale;
        double petHeight = Sprite.Height * settings.PetScale;
        double newWidth = Math.Max(MinWidth_, petWidth + 20);
        double newHeight = BubbleArea + petHeight;

        if (initial)
        {
            Width = newWidth;
            Height = newHeight;
            var area = SystemParameters.WorkArea;
            Left = settings.PositionX ?? area.Right - newWidth - 20;
            Top = settings.PositionY ?? area.Bottom - newHeight - 4;
        }
        else
        {
            // Keep the pet's feet where they are when the size changes.
            double bottom = Top + Height, center = Left + Width / 2;
            Width = newWidth;
            Height = newHeight;
            Left = center - newWidth / 2;
            Top = bottom - newHeight;
        }
        ClampToScreen();

        PetImage.Width = petWidth;
        PetImage.Height = petHeight;

        if (_animations) _animationTimer.Start();
        else _animationTimer.Stop();
        Render();
    }

    public void SetMood(PetMood mood, bool working)
    {
        if (mood == _mood && working == _working) return;
        _mood = mood;
        _working = working;
        Render();
    }

    /// <summary>Short happy animation, e.g. after a reset.</summary>
    public void Cheer(TimeSpan duration)
    {
        _cheerUntil = DateTime.Now + duration;
        Render();
    }

    public void Say(string text, double seconds)
    {
        BubbleText.Text = text;
        Bubble.Visibility = Visibility.Visible;
        Bubble.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)));
        _bubbleTimer.Stop();
        _bubbleTimer.Interval = TimeSpan.FromSeconds(seconds);
        _bubbleTimer.Start();
    }

    public void HideBubble()
    {
        _bubbleTimer.Stop();
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(250));
        fade.Completed += (_, _) =>
        {
            if (!_bubbleTimer.IsEnabled) Bubble.Visibility = Visibility.Collapsed;
        };
        Bubble.BeginAnimation(OpacityProperty, fade);
    }

    /// <summary>Screen rectangle (in DIPs) of the pet sprite itself.</summary>
    public Rect PetBounds
    {
        get
        {
            var topLeft = PetImage.TranslatePoint(new Point(0, 0), this);
            return new Rect(Left + topLeft.X, Top + topLeft.Y, PetImage.Width, PetImage.Height);
        }
    }

    private void Render()
    {
        bool cheering = DateTime.Now < _cheerUntil;
        long t = _animations ? _tick : 1;
        var frame = PetAnimator.Frame(_mood, _working, cheering, t);
        if (_lookAt is { } eyes) frame = frame with { Eyes = eyes };
        PetImage.Source = Sprite.Render(frame);
    }

    private void ClampToScreen()
    {
        double left = SystemParameters.VirtualScreenLeft;
        double top = SystemParameters.VirtualScreenTop;
        double right = left + SystemParameters.VirtualScreenWidth;
        double bottom = top + SystemParameters.VirtualScreenHeight;
        Left = Math.Clamp(Left, left - Width / 2, right - Width / 2);
        Top = Math.Clamp(Top, top - BubbleArea, bottom - Height);
    }

    private void Pet_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            _clickTimer.Stop();
            _pressPoint = null;
            e.Handled = true;
            DoubleClicked?.Invoke();
            return;
        }
        _pressPoint = e.GetPosition(this);
        PetImage.CaptureMouse();
        e.Handled = true;
    }

    private void Pet_MouseMove(object sender, MouseEventArgs e)
    {
        if (_rightPressPoint is { } rightStart && e.RightButton == MouseButtonState.Pressed)
        {
            var moved = e.GetPosition(this) - rightStart;
            if (Math.Abs(moved.X) >= 4 || Math.Abs(moved.Y) >= 4) StartGhostDrag();
            return;
        }
        if (_pressPoint is not { } start || e.LeftButton != MouseButtonState.Pressed) return;
        var delta = e.GetPosition(this) - start;
        if (Math.Abs(delta.X) < 4 && Math.Abs(delta.Y) < 4) return;

        _pressPoint = null;
        PetImage.ReleaseMouseCapture();
        try { DragMove(); } catch (InvalidOperationException) { }
        ClampToScreen();
        Moved?.Invoke();
    }

    private void Pet_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        PetImage.ReleaseMouseCapture();
        if (_pressPoint == null) return;
        _pressPoint = null;
        _clickedAt = DateTime.Now;
        _clickTimer.Start();
    }

    // ---------------------------------------------------------------- ghost drag

    private void Pet_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _suppressContextMenu = false;
        _rightPressPoint = _ghostDrag ? e.GetPosition(this) : null;
    }

    private void Pet_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        _rightPressPoint = null;
        if (_ghost != null) EndGhostDrag(drop: true);
    }

    private void Pet_LostMouseCapture(object sender, MouseEventArgs e)
    {
        // e.g. Alt+Tab or a system dialog while dragging
        if (_ghost != null) EndGhostDrag(drop: false);
    }

    private void Pet_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        // The right button release that ends a ghost drag must not open the menu.
        if (!_suppressContextMenu) return;
        _suppressContextMenu = false;
        e.Handled = true;
    }

    private void StartGhostDrag()
    {
        _rightPressPoint = null;
        _suppressContextMenu = true;
        _clickTimer.Stop();

        _ghost = new GhostWindow(_scale) { Left = -10000, Top = -10000 };
        _ghost.Show();
        PetImage.CaptureMouse();
        UpdateGhost();
        _ghostTimer.Start();
    }

    private void UpdateGhost()
    {
        if (_ghost == null) return;
        if (GetAsyncKeyState(VK_ESCAPE) < 0)
        {
            EndGhostDrag(drop: false);
            return;
        }

        GetCursorPos(out var cursor);
        _ghost.MoveTo(cursor.X, cursor.Y);
        _ghost.SetHighlight(ExplorerLocator.IsShellAt(cursor.X, cursor.Y, _ghost.Handle));

        // The pet watches its ghost.
        var center = PetImage.PointToScreen(new Point(PetImage.ActualWidth / 2, 0));
        var eyes = cursor.X < center.X ? Eyes.LookLeft : Eyes.LookRight;
        if (_lookAt != eyes)
        {
            _lookAt = eyes;
            Render();
        }
    }

    private void EndGhostDrag(bool drop)
    {
        if (_ghost is not { } ghost) return;
        _ghost = null;               // before releasing capture: LostMouseCapture must not end it twice
        _ghostTimer.Stop();
        PetImage.ReleaseMouseCapture();
        _lookAt = null;
        Render();

        string? folder = null;
        bool isShell = false;
        if (drop)
        {
            GetCursorPos(out var cursor);
            folder = ExplorerLocator.FolderAt(cursor.X, cursor.Y, ghost.Handle, out isShell);
        }
        ghost.SetHighlight(folder != null);
        ghost.Vanish();
        if (drop) GhostDropped?.Invoke(folder, isShell);
    }

    private void Bubble_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => HideBubble();

    private void HideFromAltTab()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int style = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, (style | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;

    private const int VK_ESCAPE = 0x1B;

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }

    [DllImport("user32.dll")] private static extern uint GetDoubleClickTime();
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
