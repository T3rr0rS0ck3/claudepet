using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Claudius.Core;
using Claudius.Shared;
using Forms = System.Windows.Forms;

namespace Claudius.Pet;

public partial class PetWindow : Window
{
    private const double BubbleArea = 130;
    private const double MinWidth_ = 260;
    /// <summary>Height of the emote buttons plus their gap to the pet.</summary>
    private const double EmoteBarArea = 34;

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
    private bool _petOnTop;
    private bool _listening;
    private Mark _sessionMark = Mark.None;
    private Outfit _outfit = Outfit.None;
    private Accessory _accessory = Accessory.None;
    private bool _night;
    private IReadOnlyList<SessionView> _sessions = [];
    private readonly Dictionary<string, BabyPetWindow> _babies = new();
    private readonly DispatcherTimer _babyTimer = new() { Interval = TimeSpan.FromMilliseconds(1000.0 / 30) };
    private readonly Stopwatch _babyClock = Stopwatch.StartNew();
    private TimeSpan _lastBabyTick;
    private bool _topmost = true;
    private (Motion Motion, int Direction) _pose = (Motion.Idle, 1);
    private Eyes? _lookAt;
    private Eyes? _gaze;
    private bool _sad;
    /// <summary>Night: the pet goes to bed (on the taskbar, or right where it is if it does not walk around).</summary>
    private bool _bedtime;
    private bool _shownInBed;
    private BedWindow? _bedWindow;
    private double _lastPetWidth;
    /// <summary>Picked up while asleep: by the blanket the bed comes along, by the head the pet comes out.</summary>
    private bool _carryingBed, _pulledOut;
    private DateTime _groggyUntil, _wakeUntil;
    private readonly DispatcherTimer _wakeTimer = new() { Interval = TimeSpan.FromSeconds(1.6) };
    private readonly Random _bedRandom = new();
    private bool _emotesEnabled = true;
    private Emote? _emote;
    private DateTime _emoteStart;
    // Leaving the pet towards the emote buttons (or back) must not hide them on the way.
    private readonly DispatcherTimer _emoteHideTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };

    /// <summary>Left click without dragging; carries the time the button was released.</summary>
    public event Action<DateTime>? Clicked;
    /// <summary>Left double-click.</summary>
    public event Action? DoubleClicked;
    /// <summary>
    /// The ghost was dropped: the folder under it (null if none) and whether it was over Explorer
    /// or the desktop at all.
    /// </summary>
    public event Action<string?, bool>? GhostDropped;
    /// <summary>The "?" or a baby pet was clicked: bring that session's window to the front.</summary>
    public event Action<SessionView>? SessionClicked;
    /// <summary>The pet was dragged to a new position.</summary>
    public event Action? Moved;
    /// <summary>One of the emote buttons was clicked; the pet already reacts to it.</summary>
    public event Action<Emote>? Emoted;

    public PetWindow()
    {
        InitializeComponent();
        _animationTimer.Tick += (_, _) => { _tick++; Render(); RenderBabies(); };
        _bubbleTimer.Tick += (_, _) => HideBubble();
        _emoteHideTimer.Tick += (_, _) =>
        {
            _emoteHideTimer.Stop();
            if (!PetImage.IsMouseOver && !EmoteBar.IsMouseOver) HideEmotes();
        };
        _walkTimer.Tick += (_, _) => Walk();
        _babyTimer.Tick += (_, _) => FollowWithBabies();
        Closed += (_, _) =>
        {
            foreach (var baby in _babies.Values) baby.Close();
            CloseBed();
        };
        IsVisibleChanged += (_, _) => { if (!IsVisible) _bedWindow?.Hide(); };
        BedBackImage.Source = Sprite.RenderBed(BedLayer.Back);
        BedFrontImage.Source = Sprite.RenderBed(BedLayer.Front);
        _wakeTimer.Tick += (_, _) =>
        {
            _wakeTimer.Stop();
            EndBedtime();
        };
        _ghostTimer.Tick += (_, _) => UpdateGhost();
        _clickTimer.Tick += (_, _) =>
        {
            _clickTimer.Stop();
            if (InBed)
            {
                // Woken up for a moment: a sleepy blink, then back to sleep.
                _groggyUntil = DateTime.Now.AddSeconds(2);
                Render();
            }
            Clicked?.Invoke(_clickedAt);
        };
        SourceInitialized += (_, _) => HideFromAltTab();
        ApplyLanguage();
    }

    /// <summary>Tooltips of the emote buttons in the current language.</summary>
    public void ApplyLanguage()
    {
        foreach (var button in EmoteBar.Children.OfType<Button>())
            if (button.Tag is string tag && Enum.TryParse(tag, out Emote emote)) button.ToolTip = Strings.EmoteName(emote);
    }

    public void ApplySettings(AppSettings settings, bool initial = false)
    {
        Topmost = _topmost = settings.AlwaysOnTop;
        foreach (var baby in _babies.Values)
        {
            baby.Topmost = _topmost;
            baby.SetScale(settings.PetScale);
        }
        _animations = settings.Animations;
        _ghostDrag = settings.GhostDrag;
        _emotesEnabled = settings.Emotes;
        if (!_emotesEnabled) HideEmotes();
        _scale = settings.PetScale;

        double petWidth = Sprite.Width * settings.PetScale;
        double petHeight = Sprite.Height * settings.PetScale;
        double tearsWidth = (Sprite.Width + 2 * Sprite.TearMargin) * settings.PetScale;
        double newWidth = Math.Max(MinWidth_, tearsWidth + 20);
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
        TearsImage.Width = tearsWidth;
        TearsImage.Height = petHeight;
        BedBackImage.Width = BedFrontImage.Width = petWidth;
        BedBackImage.Height = BedFrontImage.Height = petHeight;
        if (_petOnTop) SetPetOnTop(true); // the bubble below the pet moves with its size

        if (_animations) _animationTimer.Start();
        else _animationTimer.Stop();

        _walking = settings.WalkAround && _animations;
        _walker.CrossMonitors = settings.CrossMonitors;
        _walker.ChaseCursor = settings.ChaseCursor;
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
            if (_petOnTop) SetPetOnTop(false);
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

    /// <summary>What the pet wears, e.g. the outfit for the AI model in use.</summary>
    public void SetOutfit(Outfit outfit)
    {
        if (outfit == _outfit) return;
        _outfit = outfit;
        Render();
    }

    /// <summary>Headwear for the season or the night; the babies wear it too.</summary>
    public void SetAccessory(Accessory accessory)
    {
        if (accessory == _accessory) return;
        _accessory = accessory;
        Render();
        RenderBabies();
    }

    /// <summary>Night: calm moods get sleepy and walk slower.</summary>
    public void SetNight(bool night)
    {
        if (night == _night) return;
        _night = night;
        _walker.Sleepy = night;
        if (night)
        {
            _bedtime = true;
            _wakeTimer.Stop();
            _wakeUntil = default;
        }
        else if (_bedtime)
        {
            // Morning: a stretch in bed first, then the bed goes; out of bed it just goes.
            if (InBed)
            {
                _wakeUntil = DateTime.Now + _wakeTimer.Interval;
                _wakeTimer.Start();
            }
            else EndBedtime();
        }
        Render();
        RenderBabies();
    }

    /// <summary>Lying in its bed: once it got there, or right away where it is while it does not walk around.</summary>
    private bool InBed => _bedtime && (_dragging ? _carryingBed : !_walking || _walker.InBed || _carryingBed);

    private void EndBedtime()
    {
        _bedtime = false;
        _carryingBed = false;
        _walker.BedX = null;
        CloseBed();
        Render();
    }

    private void CloseBed()
    {
        _bedWindow?.Close();
        _bedWindow = null;
    }

    /// <summary>
    /// At night: picks a spot for the bed on the taskbar and shows the bed there until the pet lies in it,
    /// e.g. while it walks over or after it was carried off.
    /// </summary>
    private void UpdateBed(double petWidth)
    {
        if (!_bedtime) return;
        if (_walking && _walker.BedX == null) _walker.BedX = BedSpot(petWidth);
        if (_carryingBed && !_dragging && _walker.InBed) _carryingBed = false; // landed in it
        bool inBed = InBed;
        if (inBed != _shownInBed)
        {
            _shownInBed = inBed;
            Render();
        }
        if (inBed || !_walking || !IsVisible || _walker.BedX is not { } x)
        {
            _bedWindow?.Hide();
            return;
        }
        if (_surfaces.Platforms.Where(p => p.IsFloor && p.Contains(x)).Select(p => (double?)p.Y).FirstOrDefault() is not { } floor)
            return;
        if (_bedWindow == null || _bedWindow.Scale != _scale)
        {
            CloseBed();
            _bedWindow = new BedWindow(_scale, _topmost);
        }
        if (!_bedWindow.IsVisible) _bedWindow.Show();
        _bedWindow.PlaceAt(x, floor, new WindowInteropHelper(this).Handle);
    }

    /// <summary>A random spot on the taskbar of the pet's monitor, not too far from it.</summary>
    private double BedSpot(double petWidth)
    {
        double x = _walker.X;
        var area = _surfaces.WorkAreas.FirstOrDefault(a => x >= a.Left && x <= a.Right);
        var floors = _surfaces.Platforms
            .Where(p => p.IsFloor && p.X2 - p.X1 > 3 * petWidth && p.X1 < area.Right && p.X2 > area.Left)
            .ToList();
        if (floors.Count == 0) return x;
        var floor = floors[_bedRandom.Next(floors.Count)];
        double lo = Math.Max(floor.X1 + petWidth, x - 15 * petWidth), hi = Math.Min(floor.X2 - petWidth, x + 15 * petWidth);
        return lo < hi ? lo + _bedRandom.NextDouble() * (hi - lo) : (floor.X1 + floor.X2) / 2;
    }

    /// <summary>In bed: asleep, a sleepy blink after a click, a stretch when it wakes up in the morning.</summary>
    private SpriteFrame BedFrame(long t)
    {
        var now = DateTime.Now;
        if (now < _wakeUntil) return new SpriteFrame(Eyes.Happy, Mouth.Open, Arms.Up);
        if (now < _groggyUntil) return new SpriteFrame(t % 12 < 9 ? Eyes.Blink : Eyes.Closed);
        var z = (t / 8 % 3) switch { 0 => Mark.Zzz1, 1 => Mark.Zzz2, _ => Mark.Zzz3 };
        return new SpriteFrame(Eyes.Closed, Bob: (int)(t / 12 % 2), Mark: z);
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
        UpdateLayout();
        UpdateBubblePlacement();
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
            if (_bubbleTimer.IsEnabled) return;
            Bubble.Visibility = Visibility.Collapsed;
            UpdateBubblePlacement();
        };
        Bubble.BeginAnimation(OpacityProperty, fade);
    }

    // ---------------------------------------------------------------- the assistant sessions

    /// <summary>
    /// Shows "?" while a session waits for an answer and, if enabled, a baby pet per session that
    /// trots after the pet, with <paramref name="outfits"/> dressed for its session's model.
    /// </summary>
    public void SetSessions(IReadOnlyList<SessionView> sessions, string? overall, bool marks, bool babies, bool outfits)
    {
        _sessions = sessions;
        var mark = marks && overall == SessionStates.Question ? Mark.Question : Mark.None;
        if (mark != _sessionMark)
        {
            _sessionMark = mark;
            Render();
        }

        var wanted = marks && babies ? sessions : [];
        foreach (var id in _babies.Keys.Where(id => wanted.All(s => s.Id != id)).ToList())
        {
            _babies[id].Close();
            _babies.Remove(id);
        }
        foreach (var session in wanted)
        {
            var outfit = outfits ? Sprite.OutfitFor(session.Info.Model) : Outfit.None;
            if (_babies.TryGetValue(session.Id, out var baby))
            {
                baby.SetSession(session, outfit);
                continue;
            }
            _babies[session.Id] = baby = new BabyPetWindow(session, outfit, _scale, _topmost);
            baby.Clicked += s => SessionClicked?.Invoke(s);
            if (IsVisible) baby.Show();
        }

        if (_babies.Count > 0 && !_babyTimer.IsEnabled)
        {
            _lastBabyTick = _babyClock.Elapsed;
            _babyTimer.Start();
        }
        else if (_babies.Count == 0)
        {
            _babyTimer.Stop();
        }
        RenderBabies();
    }

    /// <summary>Babies line up behind the pet, on the same ground, and catch up when it moves.</summary>
    private void FollowWithBabies()
    {
        var now = _babyClock.Elapsed;
        double dt = Math.Clamp((now - _lastBabyTick).TotalSeconds, 0, 0.1);
        _lastBabyTick = now;

        foreach (var baby in _babies.Values)
        {
            if (IsVisible && !baby.IsVisible) baby.Show();
            else if (!IsVisible && baby.IsVisible) baby.Hide();
        }
        var hwnd = new WindowInteropHelper(this).Handle;
        if (!IsVisible || hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var rect)) return;

        var dpi = VisualTreeHelper.GetDpi(this);
        double petWidth = PetImage.Width * dpi.DpiScaleX, petHeight = PetImage.Height * dpi.DpiScaleY;
        double feetX = (rect.Left + rect.Right) / 2.0;
        double feetY = _petOnTop ? rect.Top + petHeight : rect.Bottom;
        double babyWidth = petWidth * BabyPetWindow.SizeFactor;
        int behind = -_pose.Direction;
        int i = 0;
        foreach (var baby in _babies.Values)
        {
            double x = feetX + behind * (petWidth * 0.45 + babyWidth * (0.6 + i * 0.95));
            baby.Follow(x, feetY, dt);
            i++;
        }
    }

    /// <summary>Draws the pet and its babies again, e.g. after the body color changed.</summary>
    public void Redraw()
    {
        Render();
        RenderBabies();
    }

    private void RenderBabies()
    {
        long t = _animations ? _tick : 1;
        foreach (var baby in _babies.Values) baby.Render(_mood, t, _accessory, _night);
    }

    /// <summary>The user is dictating to the assistant: stand still and listen.</summary>
    public void SetListening(bool listening)
    {
        if (listening == _listening) return;
        _listening = listening;
        Render();
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
        if (_emote is { } emote)
        {
            // Emotes run on their own clock, so they also play with animations switched off.
            long et = (long)((DateTime.Now - _emoteStart).TotalSeconds * PetAnimator.TicksPerSecond);
            if (et < PetAnimator.EmoteTicks(emote))
            {
                PetImage.Source = Sprite.Render(PetAnimator.EmoteFrame(emote, et) with { Outfit = _outfit, Accessory = _accessory });
                return;
            }
            _emote = null;
            if (!_animations) _animationTimer.Stop();
        }
        bool cheering = DateTime.Now < _cheerUntil;
        long t = _animations ? _tick : 1;
        var frame = PetAnimator.Frame(_mood, _working, cheering, t, _pose.Motion, _pose.Direction, _listening, _night);
        bool inBed = InBed;
        BedBackImage.Visibility = BedFrontImage.Visibility = inBed ? Visibility.Visible : Visibility.Collapsed;
        if (inBed) frame = BedFrame(t);
        // Eyes on the ghost being dragged, or on the pointer it is about to chase or chasing.
        if ((_lookAt ?? _gaze) is { } eyes) frame = frame with { Eyes = eyes };
        // Could not grab the pointer: eyes squeezed shut, wailing with tears spurting out to the sides.
        else if (_sad)
            frame = frame with
            {
                Eyes = Eyes.Closed, Mouth = Mouth.Wavy, Arms = Arms.Down, Legs = 0,
                Bob = (int)(t / 3 % 2), Shake = 0, Mark = Mark.None,
            };
        TearsImage.Source = _sad ? Sprite.RenderTears((int)(t % 3), frame.Bob) : null;
        TearsImage.Visibility = _sad ? Visibility.Visible : Visibility.Collapsed;
        if (_sessionMark != Mark.None) frame = frame with { Mark = _sessionMark };
        PetImage.Source = Sprite.Render(frame with { Outfit = _outfit, Accessory = _accessory });
    }

    // ---------------------------------------------------------------- walking around

    /// <summary>
    /// Pet at the top of the window, with the speech bubble hanging below it, or at the bottom with the
    /// bubble above it. Does not move the window.
    /// </summary>
    private void SetPetOnTop(bool onTop)
    {
        _petOnTop = onTop;
        Grid.SetRow(PetImage, onTop ? 0 : 1);
        PetImage.VerticalAlignment = onTop ? VerticalAlignment.Top : VerticalAlignment.Stretch;
        Grid.SetRow(TearsImage, onTop ? 0 : 1);
        TearsImage.VerticalAlignment = PetImage.VerticalAlignment;
        foreach (var bed in new[] { BedBackImage, BedFrontImage })
        {
            Grid.SetRow(bed, onTop ? 0 : 1);
            bed.VerticalAlignment = PetImage.VerticalAlignment;
        }
        LayoutOverlays();
    }

    /// <summary>
    /// Emote buttons right next to the pet and the speech bubble beyond them: above the pet, or below it
    /// while the pet is at the top of the window.
    /// </summary>
    private void LayoutOverlays()
    {
        bool onTop = _petOnTop;
        double bar = EmoteBar.Visibility == Visibility.Visible ? EmoteBarArea : 0;
        var align = onTop ? VerticalAlignment.Top : VerticalAlignment.Bottom;
        EmoteBar.VerticalAlignment = align;
        EmoteBar.Margin = onTop ? new Thickness(0, PetImage.Height + 4, 0, 0) : new Thickness(0, 0, 0, 4);
        Bubble.VerticalAlignment = align;
        Bubble.Margin = onTop ? new Thickness(0, PetImage.Height + 2 + bar, 0, 0) : new Thickness(0, 0, 0, 2 + bar);
        BubbleTailUp.Visibility = onTop ? Visibility.Visible : Visibility.Collapsed;
        BubbleTailDown.Visibility = onTop ? Visibility.Collapsed : Visibility.Visible;
        UpdateLayout();
    }

    /// <summary>
    /// Climbing, hanging and falling from the top, or too close to the top of the screen for the speech
    /// bubble above the pet: the pet goes to the top of the window and the bubble below it. Physical pixels.
    /// </summary>
    private bool WantsPetOnTop(double feetX, double feetY, double petHeight)
    {
        if (_walking && _walker.Motion is Motion.Climb or Motion.Hang or Motion.Fall) return true;
        double above = (Bubble.Visibility == Visibility.Visible ? Bubble.ActualHeight + 2 : 0)
                       + (EmoteBar.Visibility == Visibility.Visible ? EmoteBarArea : 0);
        if (above == 0) return false;
        above *= VisualTreeHelper.GetDpi(this).DpiScaleY;
        var screen = Forms.Screen.FromPoint(new System.Drawing.Point((int)feetX, (int)feetY - 1));
        return feetY - petHeight - above < screen.WorkingArea.Top;
    }

    /// <summary>Moves the bubble below the pet or back above it if needed, keeping the pet where it is.</summary>
    private void UpdateBubblePlacement()
    {
        // While carried the window follows the cursor; moving it here would make the pet jump away from it.
        if (_dragging) return;
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || !IsVisible || !GetWindowRect(hwnd, out var rect)) return;
        double petHeight = PetImage.Height * VisualTreeHelper.GetDpi(this).DpiScaleY;
        double feetX = (rect.Left + rect.Right) / 2.0;
        double feetY = _petOnTop ? rect.Top + petHeight : rect.Bottom;
        bool onTop = WantsPetOnTop(feetX, feetY, petHeight);
        if (onTop == _petOnTop) return;
        SetPetOnTop(onTop);
        int y = (int)Math.Round(feetY - (onTop ? petHeight : rect.Bottom - rect.Top));
        SetWindowPos(hwnd, IntPtr.Zero, rect.Left, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    private void Walk()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || !IsVisible || !GetWindowRect(hwnd, out var rect)) return;
        var now = _walkClock.Elapsed;
        double dt = Math.Clamp((now - _lastTick).TotalSeconds, 0, 0.1);
        _lastTick = now;
        if (_dragging || _paused) return;

        // Hold still under the mouse so clicks and double-clicks land on the pet; a fall or jump still finishes.
        bool aiming = PetImage.IsMouseOver || EmoteBar.IsMouseOver || _emote != null
                      || _pressPoint != null || _clickTimer.IsEnabled;
        if (aiming && !_needsPlace && _walker.Motion is not (Motion.Fall or Motion.Jump or Motion.Splat))
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
            // Carried in its bed, or set down again for another reason while in it: it lands in it.
            _walker.Place(rect.Left + width / 2, _petOnTop ? rect.Top + petHeight : rect.Bottom,
                _carryingBed || _walker.InBed && !_pulledOut);
            _pulledOut = false; // _carryingBed lasts until it has landed, so the bed falls with it
            _needsPlace = false;
        }

        bool canWalk = !_listening && !_surfaces.IsFullscreen(_walker.X, _walker.Y);
        GetCursorPos(out var cursor);
        _walker.Step(dt, _surfaces, _mood, canWalk, petWidth, petHeight, (cursor.X, cursor.Y));
        _lastPetWidth = petWidth;
        UpdateBed(petWidth);
        if (_walker.Gaze != _gaze || _walker.Sad != _sad)
        {
            _gaze = _walker.Gaze;
            _sad = _walker.Sad;
            Render();
        }

        // Climbing, hanging and falling from the top: pet at the top of the window, so the
        // (transparent) speech bubble area does not stick out above the screen.
        bool petOnTop = WantsPetOnTop(_walker.X, _walker.Y, petHeight);
        if (petOnTop != _petOnTop) SetPetOnTop(petOnTop);
        double feetToTop = petOnTop ? petHeight : height;
        int x = (int)Math.Round(_walker.X - width / 2), y = (int)Math.Round(_walker.Y - feetToTop);
        if (x != rect.Left || y != rect.Top)
            SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

        var pose = (_walker.Motion, _walker.Direction);
        if (pose != _pose)
        {
            if (pose.Motion == Motion.Splat) Squash();
            _pose = pose;
            Render();
        }
    }

    /// <summary>Squashes the pet flat onto the ground after a long fall and lets it spring back into shape.</summary>
    private void Squash()
    {
        // Spread out as far as the window allows; it is only a little wider than the pet at large sizes.
        double wide = Math.Min(1.45, ActualWidth / PetImage.Width);
        var squash = new ScaleTransform();
        PetImage.RenderTransformOrigin = new Point(0.5, 1); // the feet stay on the ground
        PetImage.RenderTransform = squash;
        squash.BeginAnimation(ScaleTransform.ScaleXProperty, SquashCurve(wide));
        squash.BeginAnimation(ScaleTransform.ScaleYProperty, SquashCurve(0.35));
    }

    /// <summary>Hits <paramref name="flat"/> on impact, stays there a moment, then wobbles back to 1.</summary>
    private static DoubleAnimationUsingKeyFrames SquashCurve(double flat)
    {
        double seconds = PetWalker.SplatSeconds;
        return new DoubleAnimationUsingKeyFrames
        {
            KeyFrames =
            {
                new LinearDoubleKeyFrame(flat, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.06))),
                new LinearDoubleKeyFrame(flat, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(seconds * 0.4))),
                new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(seconds)),
                    new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 2, Springiness = 4 }),
            },
        };
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

    // Left drag: ghost onto an Explorer window (or move the pet if the ghost is switched off).
    // Right drag: move the pet. A plain left click opens the overlay, a right click the menu.
    private void Pet_MouseMove(object sender, MouseEventArgs e)
    {
        if (_moveOffset != null)
        {
            MoveWithCursor();
            return;
        }
        if (_rightPressPoint is { } rightStart && e.RightButton == MouseButtonState.Pressed)
        {
            if (Moved4(e.GetPosition(this) - rightStart)) StartMove();
            return;
        }
        if (_pressPoint is not { } start || e.LeftButton != MouseButtonState.Pressed) return;
        if (!Moved4(e.GetPosition(this) - start)) return;

        _pressPoint = null;
        if (_ghostDrag) StartGhostDrag();
        else StartMove();
    }

    /// <summary>The session that asked last if <paramref name="point"/> (on the pet image) is above the head, where the "?" is.</summary>
    private SessionView? QuestionAt(Point point)
    {
        if (_sessionMark != Mark.Question || _emote != null) return null;
        if (point.Y >= PetImage.ActualHeight * QuestionRows / Sprite.Height) return null;
        return _sessions.LastOrDefault(s => s.State == SessionStates.Question);
    }

    /// <summary>Sprite rows above the head, where the "?" is drawn.</summary>
    private const int QuestionRows = 5;

    private static bool Moved4(Vector delta) => Math.Abs(delta.X) >= 4 || Math.Abs(delta.Y) >= 4;

    private void Pet_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_ghost != null)
        {
            EndGhostDrag(drop: true);
            return;
        }
        if (_moveOffset != null)
        {
            EndMove();
            return;
        }
        PetImage.ReleaseMouseCapture();
        if (_pressPoint == null) return;
        _pressPoint = null;
        // A click on the "?" goes to the session asking, right away: Windows lets the window come to the
        // front only while the click is the last input.
        if (QuestionAt(e.GetPosition(PetImage)) is { } session)
        {
            SessionClicked?.Invoke(session);
            return;
        }
        _clickedAt = DateTime.Now;
        _clickTimer.Start();
    }

    private void Pet_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _suppressContextMenu = false;
        _rightPressPoint = e.GetPosition(this);
    }

    private void Pet_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        _rightPressPoint = null;
        if (_moveOffset != null) EndMove();
    }

    private void Pet_LostMouseCapture(object sender, MouseEventArgs e)
    {
        // e.g. Alt+Tab or a system dialog while dragging
        if (_ghost != null) EndGhostDrag(drop: false);
        else if (_moveOffset != null) EndMove();
    }

    private void Pet_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        // The right button release that ends a drag must not open the menu.
        if (!_suppressContextMenu) return;
        _suppressContextMenu = false;
        e.Handled = true;
    }

    // ---------------------------------------------------------------- moving the pet

    /// <summary>Cursor position relative to the window's top left while the pet is carried (physical pixels).</summary>
    private POINT? _moveOffset;

    private void StartMove()
    {
        HideEmotes(); // first: it may move the window
        var hwnd = new WindowInteropHelper(this).Handle;
        GetCursorPos(out var cursor);
        if (!GetWindowRect(hwnd, out var rect)) return;
        _rightPressPoint = null;
        _suppressContextMenu = true;
        _clickTimer.Stop();
        _moveOffset = new POINT { X = cursor.X - rect.Left, Y = cursor.Y - rect.Top };
        // Picked up asleep, it wakes a little. Grabbed by the blanket, the bed comes along to wherever it is put
        // down; grabbed by the head, the pet comes out and walks back to bed afterwards.
        if (InBed)
        {
            _carryingBed = Mouse.GetPosition(PetImage).Y >= PetImage.ActualHeight * Sprite.BlanketTop / Sprite.Height;
            _pulledOut = !_carryingBed;
            _groggyUntil = DateTime.Now.AddSeconds(2);
        }
        _dragging = true;
        UpdateBed(_lastPetWidth);
        PetImage.CaptureMouse();
        PetImage.RenderTransform = Transform.Identity; // picked up while squashed flat
        _pose = (Motion.Carried, _pose.Direction);
        Render();
    }

    private void MoveWithCursor()
    {
        if (_moveOffset is not { } offset) return;
        GetCursorPos(out var cursor);
        SetWindowPos(new WindowInteropHelper(this).Handle, IntPtr.Zero, cursor.X - offset.X, cursor.Y - offset.Y, 0, 0,
            SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    private void EndMove()
    {
        if (_moveOffset == null) return;
        _moveOffset = null;          // before releasing capture: LostMouseCapture must not end it twice
        PetImage.ReleaseMouseCapture();
        _dragging = false;
        if (!_walking) _pose = (Motion.Idle, _pose.Direction);
        Render();
        _needsPlace = true; // let go: fall down from here
        ClampToScreen();
        UpdateBubblePlacement();
        Moved?.Invoke();
    }

    // ---------------------------------------------------------------- ghost drag

    private void StartGhostDrag()
    {
        HideEmotes();
        _rightPressPoint = null;
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

    // ---------------------------------------------------------------- emotes

    private void Pet_MouseEnter(object sender, MouseEventArgs e)
    {
        _emoteHideTimer.Stop();
        if (!_emotesEnabled || _dragging || _ghost != null || EmoteBar.Visibility == Visibility.Visible) return;
        EmoteBar.Visibility = Visibility.Visible;
        LayoutOverlays();
        UpdateBubblePlacement();
    }

    private void Emotes_MouseLeave(object sender, MouseEventArgs e)
    {
        _emoteHideTimer.Stop();
        _emoteHideTimer.Start();
    }

    private void HideEmotes()
    {
        _emoteHideTimer.Stop();
        if (EmoteBar.Visibility != Visibility.Visible) return;
        EmoteBar.Visibility = Visibility.Collapsed;
        LayoutOverlays();
        UpdateBubblePlacement();
    }

    private void Emote_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag } || !Enum.TryParse(tag, out Emote emote)) return;
        PlayEmote(emote);
        Emoted?.Invoke(emote);
    }

    /// <summary>Plays the pet's reaction to an emote; playing also makes it hop.</summary>
    public void PlayEmote(Emote emote)
    {
        _emote = emote;
        _emoteStart = DateTime.Now;
        _animationTimer.Start();
        if (emote == Emote.Play && !_petOnTop)
        {
            var shift = new TranslateTransform();
            PetImage.RenderTransform = shift;
            shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -PetImage.Height * 0.4, TimeSpan.FromSeconds(0.25))
            {
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(3),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            });
        }
        Render();
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
