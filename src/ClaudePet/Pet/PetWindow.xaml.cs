using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
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
    private readonly DispatcherTimer _walkTimer = new() { Interval = TimeSpan.FromMilliseconds(1000.0 / 30) };
    private readonly Stopwatch _walkClock = new();
    private readonly PetWalker _walker = new();
    private readonly DesktopSurfaces _surfaces = new();
    private TimeSpan _lastScan;
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
    private TimeSpan _lastTick;
    private Point? _pressPoint;
    private bool _walking;
    private bool _dragging;
    private bool _paused;
    private bool _needsPlace = true;
    private (Motion Motion, int Direction) _pose = (Motion.Idle, 1);
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
        _walkTimer.Tick += (_, _) => Walk();
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

        _walking = settings.WalkAround && _animations;
        _needsPlace = true;
        if (_walking)
        {
            _walkClock.Restart();
            _lastTick = _lastScan = TimeSpan.Zero;
            _walkTimer.Start();
        }
        else
        {
            _walkTimer.Stop();
            _pose = (Motion.Idle, 1);
        }
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

    /// <summary>Stops walking around while e.g. the usage overlay is attached to the pet.</summary>
    public void PauseWalking(bool paused)
    {
        _paused = paused;
        if (!paused) _needsPlace = true;
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
        var frame = PetAnimator.Frame(_mood, _working, cheering, t, _pose.Motion, _pose.Direction);
        if (_lookAt is { } eyes) frame = frame with { Eyes = eyes };
        PetImage.Source = Sprite.Render(frame);
    }

    // ---------------------------------------------------------------- walking around

    private void Walk()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || !IsVisible || !GetWindowRect(hwnd, out var rect)) return;
        var now = _walkClock.Elapsed;
        double dt = Math.Clamp((now - _lastTick).TotalSeconds, 0, 0.1);
        _lastTick = now;
        if (_dragging || _paused) return;

        // Hold still under the mouse so clicks and double-clicks land on the pet; a fall or jump still finishes.
        bool aiming = PetImage.IsMouseOver || _pressPoint != null || _clickTimer.IsEnabled;
        if (aiming && !_needsPlace && _walker.Motion is not (Motion.Fall or Motion.Jump))
        {
            if (_pose.Motion is Motion.Walk or Motion.Run)
            {
                _pose = (Motion.Idle, _pose.Direction);
                Render();
            }
            return;
        }

        // Everything below is in physical pixels; the window's bottom edge is where the feet are.
        var dpi = VisualTreeHelper.GetDpi(this);
        double petWidth = PetImage.Width * dpi.DpiScaleX, petHeight = PetImage.Height * dpi.DpiScaleY;
        if (_needsPlace || now - _lastScan > TimeSpan.FromMilliseconds(250))
        {
            _surfaces.Scan(petWidth, petHeight);
            _lastScan = now;
        }
        double width = rect.Right - rect.Left, height = rect.Bottom - rect.Top;
        if (_needsPlace)
        {
            _walker.Place(rect.Left + width / 2, rect.Bottom);
            _needsPlace = false;
        }

        bool canWalk = !_surfaces.IsFullscreen(_walker.X, _walker.Y);
        _walker.Headroom = height;
        _walker.Step(dt, _surfaces, _mood, canWalk, petWidth, petHeight);

        int x = (int)Math.Round(_walker.X - width / 2), y = (int)Math.Round(_walker.Y - height);
        if (x != rect.Left || y != rect.Top)
            SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

        var pose = (_walker.Motion, _walker.Direction);
        if (pose != _pose)
        {
            _pose = pose;
            Render();
        }
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
        _dragging = true;
        try { DragMove(); } catch (InvalidOperationException) { }
        _dragging = false;
        _needsPlace = true; // let go: fall down from here
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

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
}
