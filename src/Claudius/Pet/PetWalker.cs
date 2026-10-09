using Claudius.Core;

namespace Claudius.Pet;

/// <summary>What the pet is doing; <see cref="Carried"/> is set by the window while it is dragged.</summary>
public enum Motion { Idle, Walk, Run, Fall, Jump, Climb, Hang, Splat, Carried }

/// <summary>
/// Moves the pet across the desktop: walks along the taskbar and window tops, falls off edges
/// and lost windows, now and then jumps up onto a nearby window, climbs up the screen edges and
/// swings along the top of the screen until it drops. After a long fall it lands with a splat.
/// It stays on the monitor it is on unless <see cref="CrossMonitors"/> is set: then it walks or hops over to a
/// neighbouring monitor where the two touch, following their actual arrangement. Works in physical pixels;
/// (X, Y) is the point between the pet's feet.
/// </summary>
public sealed class PetWalker
{
    /// <summary>Speed in pet widths per second, pause and walk durations in seconds.</summary>
    private readonly record struct Gait(double Speed, double IdleMin, double IdleMax, double WalkMin, double WalkMax, double JumpChance);

    private static Gait For(PetMood mood) => mood switch
    {
        // Short pauses, long walks: the pet is on the move most of the time.
        PetMood.Relaxed => new(0.5, 0.5, 2, 4, 12, 0.35),
        PetMood.Normal => new(0.6, 0.5, 2, 4, 12, 0.3),
        PetMood.Attentive => new(0.8, 0.4, 1.5, 4, 10, 0.25),
        PetMood.Nervous => new(1.1, 0.3, 1, 3, 8, 0.2),
        PetMood.Worried => new(1.4, 0.2, 0.8, 3, 8, 0.15),
        PetMood.Panic => new(2.2, 0.1, 0.4, 2, 6, 0.1),
        PetMood.Exhausted => new(0.25, 1, 3, 3, 8, 0), // sleepwalking
        _ => new(0.45, 0.5, 2, 4, 10, 0.3), // Unknown: wandering around, looking for data
    };

    private const double FallOffChance = 0.6;
    private const double EdgeJumpChance = 0.5;
    private const double ClimbChance = 0.6;
    private const double HangChance = 0.75;
    private const double CrossChance = 0.6;
    /// <summary>How far up the floor of the next monitor may be for a hop across, in pet heights.</summary>
    private const double CrossReachUp = 2.5;
    /// <summary>Feet below the top of the screen while hanging, in pet heights (arms reach up to row 6 of 16).</summary>
    public const double HangDepth = 10.0 / 16;
    /// <summary>Distance from the pet's center to its side, in pet widths.</summary>
    private const double HalfBody = 0.45;
    /// <summary>A fall at least this deep, in pet heights, squashes the pet flat on landing.</summary>
    private const double SplatHeight = 3;
    /// <summary>How long the pet lies flat and pulls itself together again, in seconds.</summary>
    public const double SplatSeconds = 0.9;

    private readonly Random _random = new();
    private bool _airborne = true;
    private double _vx, _vy;
    private double _timer = 1;
    private IntPtr _ground;
    private ScreenRect _groundRect;
    private int _checkedVersion;
    private bool _climbing;
    private bool _hanging;
    /// <summary>Monitor (work area) the pet is hopping over to; it may leave the current one while flying.</summary>
    private ScreenRect? _crossTo;
    /// <summary>Walking over onto the next monitor's taskbar; decided once at the edge.</summary>
    private bool _crossing;
    /// <summary>Where the current fall started; NaN while jumping, which never ends in a splat.</summary>
    private double _fallFrom = double.NaN;
    /// <summary>Seconds left lying flat after a long fall.</summary>
    private double _splat;

    /// <summary>Walk and hop over to neighbouring monitors.</summary>
    public bool CrossMonitors { get; set; }

    /// <summary>Night: in calm moods the pet walks slower, pauses longer and does not jump.</summary>
    public bool Sleepy { get; set; }

    private Gait GaitFor(PetMood mood)
    {
        var gait = GaitFor(mood);
        return Sleepy && mood is PetMood.Relaxed or PetMood.Normal or PetMood.Attentive
            ? gait with { Speed = gait.Speed * 0.6, IdleMin = gait.IdleMin * 1.5, IdleMax = gait.IdleMax * 1.5, JumpChance = 0 }
            : gait;
    }

    public double X { get; private set; }
    public double Y { get; private set; }
    public int Direction { get; private set; } = 1;
    public Motion Motion { get; private set; }

    /// <summary>Drops the pet at the given position; it falls until it lands on something.</summary>
    public void Place(double x, double y)
    {
        X = x;
        Y = y;
        _airborne = true;
        _climbing = _hanging = false;
        _fallFrom = y;
        _splat = 0;
        _vx = _vy = 0;
        _ground = IntPtr.Zero;
        _crossTo = null;
        Motion = Motion.Fall;
    }

    public void Step(double dt, DesktopSurfaces surfaces, PetMood mood, bool canWalk, double petWidth, double petHeight)
    {
        if (surfaces.WorkAreas.Count == 0) return;
        if (_airborne)
        {
            Fly(dt, surfaces, petWidth, petHeight);
            return;
        }
        if (_splat > 0)
        {
            // Lies flat where it landed, riding along if the window moves.
            _splat -= dt;
            if (KeepFooting(surfaces)) Motion = _splat > 0 ? Motion.Splat : Motion.Idle;
            return;
        }
        if (_hanging)
        {
            Hang(dt, surfaces, GaitFor(mood), canWalk, petWidth, petHeight);
            return;
        }
        if (_climbing)
        {
            Climb(dt, surfaces, GaitFor(mood), canWalk, petWidth, petHeight);
            return;
        }
        if (!KeepFooting(surfaces)) return;

        if (!canWalk)
        {
            Motion = Motion.Idle;
            return;
        }

        var gait = GaitFor(mood);
        _timer -= dt;
        if (Motion == Motion.Idle)
        {
            if (_timer > 0) return;
            if (_random.NextDouble() < gait.JumpChance && TryJump(surfaces, petWidth, petHeight)) return;
            // Mostly keep going the same way so it gets around the whole screen.
            if (_random.NextDouble() < 0.25) Direction = -Direction;
            Motion = gait.Speed >= 1.3 ? Motion.Run : Motion.Walk;
            _timer = Between(gait.WalkMin, gait.WalkMax);
            return;
        }

        double speed = gait.Speed * petWidth;
        double nx = X + Direction * speed * dt;
        var edge = ScreenEdge(surfaces, nx, petWidth);
        if (edge == null) _crossing = false;
        if (edge is { } wall)
        {
            if (NextMonitor(surfaces, petHeight) is { } next && (_crossing || _random.NextDouble() < CrossChance))
            {
                // Its taskbar is level with the floor: just walk on. Otherwise hop across.
                if (_ground == IntPtr.Zero && Math.Abs(next.Bottom - Y) <= 2)
                {
                    _crossing = true;
                    X = nx;
                }
                else
                {
                    JumpTo(wall + Direction * petWidth * 0.8, next.Bottom, petHeight);
                    _crossTo = next;
                    return;
                }
            }
            else if (_random.NextDouble() < ClimbChance) StartClimb(wall, petWidth);
            else Direction = -Direction;
        }
        else if (Supports(surfaces, _ground, nx))
        {
            X = nx;
        }
        else if (Continuation(surfaces, nx) is { } next)
        {
            StandOn(next);
            X = nx;
        }
        else if (!Occluded(nx) && _random.NextDouble() < EdgeJumpChance && TryEdgeJump(surfaces, petWidth, petHeight))
        {
            return;
        }
        else if (!Occluded(nx) && HasPlatformBelow(surfaces, nx) && _random.NextDouble() < FallOffChance)
        {
            X = nx;
            StartFall(Direction * speed * 0.6);
            return;
        }
        else
        {
            Direction = -Direction;
        }

        if (_timer <= 0)
        {
            Motion = Motion.Idle;
            _timer = Between(gait.IdleMin, gait.IdleMax);
        }
    }

    // ---------------------------------------------------------------- standing

    /// <summary>Follows a moving window and checks the ground is still there. False if falling.</summary>
    private bool KeepFooting(DesktopSurfaces surfaces)
    {
        if (_ground == IntPtr.Zero)
        {
            var floor = surfaces.Platforms
                .Where(p => p.IsFloor && p.Contains(X))
                .OrderBy(p => Math.Abs(p.Y - Y))
                .Cast<Platform?>()
                .FirstOrDefault();
            // A couple of pixels lower is no fall, e.g. the taskbar of the next monitor.
            if (floor is not { } f || f.Y > Y + 2.5) { StartFall(0); return false; }
            Y = f.Y; // e.g. the taskbar got taller
            return true;
        }

        if (DesktopSurfaces.LiveBounds(_ground) is not { } live) { StartFall(0); return false; }
        X += live.Left - _groundRect.Left;
        Y = live.Top;
        _groundRect = live;
        if (X < live.Left || X > live.Right) { StartFall(0); return false; }

        // Covered by another window since the last scan?
        if (surfaces.Version != _checkedVersion)
        {
            _checkedVersion = surfaces.Version;
            if (surfaces.ScannedRect(_ground) == live && !surfaces.Platforms.Any(p => p.Window == _ground && p.Contains(X)))
            {
                StartFall(0);
                return false;
            }
        }
        return true;
    }

    private bool Supports(DesktopSurfaces surfaces, IntPtr ground, double x)
    {
        if (ground == IntPtr.Zero)
            return surfaces.Platforms.Any(p => p.IsFloor && Math.Abs(p.Y - Y) < 1 && p.Contains(x));
        if (x < _groundRect.Left || x > _groundRect.Right) return false;
        // While the window is being moved the scan is stale; trust the live rectangle.
        if (surfaces.ScannedRect(ground) != _groundRect) return true;
        return surfaces.Platforms.Any(p => p.Window == ground && p.Contains(x));
    }

    /// <summary>The edge of the window itself was not reached, something else is in the way.</summary>
    private bool Occluded(double x) => _ground != IntPtr.Zero && x >= _groundRect.Left && x <= _groundRect.Right;

    /// <summary>Another surface at the same height, e.g. the taskbar of the next monitor.</summary>
    private Platform? Continuation(DesktopSurfaces surfaces, double x) =>
        surfaces.Platforms
            .Where(p => Math.Abs(p.Y - Y) <= 2 && p.Contains(x) && IsThere(p))
            .Cast<Platform?>()
            .FirstOrDefault();

    /// <summary>The window behind a platform from the last scan still exists.</summary>
    private static bool IsThere(Platform p) => p.IsFloor || DesktopSurfaces.LiveBounds(p.Window) != null;

    private bool HasPlatformBelow(DesktopSurfaces surfaces, double x) =>
        surfaces.Platforms.Any(p => p.Y > Y + 2 && p.Contains(x));

    private void StandOn(Platform platform)
    {
        _ground = platform.Window;
        Y = platform.Y;
        _checkedVersion = 0;
        if (!platform.IsFloor && DesktopSurfaces.LiveBounds(platform.Window) is { } live)
        {
            _groundRect = live;
            Y = live.Top;
        }
    }

    // ---------------------------------------------------------------- screen edges

    /// <summary>The side of the current monitor the pet would bump into at <paramref name="x"/>.</summary>
    private double? ScreenEdge(DesktopSurfaces surfaces, double x, double petWidth)
    {
        var wa = WorkAreaAt(surfaces, X, Y - 1);
        double wall = Direction < 0 ? wa.Left : wa.Right;
        bool inside = Direction < 0 ? x - petWidth * HalfBody >= wall : x + petWidth * HalfBody <= wall;
        return inside ? null : wall;
    }

    /// <summary>
    /// The monitor next to the current one in the walking direction, if the two touch where the pet can get
    /// through and its taskbar is within reach.
    /// </summary>
    private ScreenRect? NextMonitor(DesktopSurfaces surfaces, double petHeight)
    {
        if (!CrossMonitors) return null;
        var here = WorkAreaAt(surfaces, X, Y - 1);
        return surfaces.WorkAreas
            .Where(next => next != here
                           && (Direction > 0 ? Math.Abs(next.Left - here.Right) <= 2 : Math.Abs(next.Right - here.Left) <= 2)
                           // The shared stretch of the edge leaves room for the pet at the higher of the two floors.
                           && Math.Max(here.Top, next.Top) <= Math.Min(Y, next.Bottom) - petHeight
                           && next.Bottom >= Y - petHeight * CrossReachUp
                           && !surfaces.IsFullscreen((next.Left + next.Right) / 2.0, next.Bottom))
            .OrderBy(next => Math.Abs(next.Bottom - Y))
            .Cast<ScreenRect?>()
            .FirstOrDefault();
    }

    private void StartClimb(double wall, double petWidth)
    {
        _climbing = true;
        X = wall - Direction * petWidth * HalfBody;
        _ground = IntPtr.Zero;
        Motion = Motion.Climb;
    }

    private void Climb(double dt, DesktopSurfaces surfaces, Gait gait, bool canWalk, double petWidth, double petHeight)
    {
        Motion = Motion.Climb;
        if (!canWalk) return;
        double ny = Y - Math.Max(gait.Speed, 0.6) * petWidth * dt;

        // A window top reaching the screen edge: step onto it.
        var ledge = surfaces.Platforms
            .Where(p => !p.IsFloor && p.Y < Y && p.Y >= ny && p.Contains(X) && IsThere(p))
            .OrderByDescending(p => p.Y)
            .Cast<Platform?>()
            .FirstOrDefault();
        if (ledge is { } l)
        {
            Direction = -Direction;
            Land(l, petHeight);
            return;
        }

        // Top of the screen: swing along it, or let go.
        var wa = WorkAreaAt(surfaces, X, Y - 1);
        if (ny - petHeight * HangDepth <= wa.Top)
        {
            if (_random.NextDouble() < HangChance) StartHang(wa, petHeight, gait);
            else StartFall(-Direction * petWidth * 0.5);
            return;
        }

        if (_random.NextDouble() < dt * 0.01) { StartFall(-Direction * petWidth * 0.3); return; }
        Y = ny;
    }

    private void StartHang(ScreenRect wa, double petHeight, Gait gait)
    {
        _climbing = false;
        _hanging = true;
        Y = wa.Top + petHeight * HangDepth;
        Direction = -Direction; // away from the wall it came up
        Motion = Motion.Hang;
        _timer = Between(Math.Max(gait.WalkMin, 3), gait.WalkMax + 6);
    }

    /// <summary>Hand over hand along the top edge of the screen; drops at random.</summary>
    private void Hang(double dt, DesktopSurfaces surfaces, Gait gait, bool canWalk, double petWidth, double petHeight)
    {
        Motion = Motion.Hang;
        if (!canWalk) return;
        _timer -= dt;
        if (_timer <= 0 || _random.NextDouble() < dt * 0.12)
        {
            StartFall(Direction * petWidth * 0.3);
            return;
        }

        var wa = WorkAreaAt(surfaces, X, Y - 1);
        Y = wa.Top + petHeight * HangDepth; // e.g. a taskbar at the top changed size
        double nx = X + Direction * Math.Max(gait.Speed * 0.7, 0.4) * petWidth * dt;
        if (Direction < 0 ? nx - petWidth * HalfBody < wa.Left : nx + petWidth * HalfBody > wa.Right)
        {
            if (_random.NextDouble() < 0.5) StartFall(0);
            else Direction = -Direction;
            return;
        }
        X = nx;
    }

    private void Land(Platform platform, double petHeight)
    {
        StandOn(platform);
        _airborne = _climbing = _hanging = false;
        _crossTo = null;
        _vx = _vy = 0;
        Motion = Motion.Idle;
        _timer = Between(0.4, 1.2);
        if (Y - _fallFrom >= petHeight * SplatHeight)
        {
            Motion = Motion.Splat;
            _splat = SplatSeconds;
        }
        _fallFrom = double.NaN;
    }

    private static ScreenRect WorkAreaAt(DesktopSurfaces surfaces, double x, double y) =>
        surfaces.WorkAreas.FirstOrDefault(wa => wa.Contains(x, y),
            surfaces.WorkAreas.MinBy(wa => Distance(wa, x, y)));

    private static double Distance(ScreenRect r, double x, double y)
    {
        double dx = Math.Max(Math.Max(r.Left - x, 0), x - r.Right);
        double dy = Math.Max(Math.Max(r.Top - y, 0), y - r.Bottom);
        return dx * dx + dy * dy;
    }

    // ---------------------------------------------------------------- airborne

    private bool TryJump(DesktopSurfaces surfaces, double petWidth, double petHeight)
    {
        // Only jump onto a window edge from beside it, not up through the window.
        var targets = surfaces.Platforms
            .Where(p => !p.IsFloor && p.Y < Y - petHeight * 0.3 && p.Y >= Y - petHeight * 4 && !p.Contains(X)
                        && Math.Min(Math.Abs(p.X1 - X), Math.Abs(p.X2 - X)) <= petWidth * 3
                        && p.X2 - p.X1 >= petWidth)
            .ToList();
        if (targets.Count == 0) return false;

        var target = targets[_random.Next(targets.Count)];
        JumpTo(X < target.X1 ? target.X1 + petWidth * 0.5 : target.X2 - petWidth * 0.5, target.Y, petHeight);
        return true;
    }

    /// <summary>At the edge of a window: hop across to the next window ahead, a bit higher or lower.</summary>
    private bool TryEdgeJump(DesktopSurfaces surfaces, double petWidth, double petHeight)
    {
        double reach = petWidth * 3;
        var target = surfaces.Platforms
            .Where(p => !p.IsFloor && p.Window != _ground && p.X2 - p.X1 >= petWidth
                        && p.Y >= Y - petHeight * 2.5 && p.Y <= Y + petHeight * 4
                        && (Direction > 0 ? p.X1 > X && p.X1 - X <= reach : p.X2 < X && X - p.X2 <= reach)
                        && IsThere(p))
            .OrderBy(p => Direction > 0 ? p.X1 - X : X - p.X2)
            .Cast<Platform?>()
            .FirstOrDefault();
        if (target is not { } t) return false;

        JumpTo(Direction > 0 ? t.X1 + petWidth * 0.5 : t.X2 - petWidth * 0.5, t.Y, petHeight);
        return true;
    }

    /// <summary>Jumps in an arc that peaks half a pet height above the higher of start and target.</summary>
    private void JumpTo(double targetX, double targetY, double petHeight)
    {
        double g = Gravity(petHeight);
        double apex = Math.Min(Y, targetY) - petHeight * 0.5;
        _vy = -Math.Sqrt(2 * g * (Y - apex));
        double time = -_vy / g + Math.Sqrt(2 * (targetY - apex) / g);
        _vx = (targetX - X) / time;
        Direction = _vx < 0 ? -1 : 1;
        _airborne = true;
        _fallFrom = double.NaN;
        _ground = IntPtr.Zero;
        Motion = Motion.Jump;
    }

    private void StartFall(double vx)
    {
        _airborne = true;
        _climbing = _hanging = false;
        _fallFrom = Y;
        _splat = 0;
        _vx = vx;
        _vy = 0;
        _ground = IntPtr.Zero;
        Motion = Motion.Fall;
    }

    private void Fly(double dt, DesktopSurfaces surfaces, double petWidth, double petHeight)
    {
        double g = Gravity(petHeight);
        _vy = Math.Min(_vy + g * dt, petHeight * 30);
        // Stay on the current monitor, or on it and the one it is hopping over to.
        var wa = WorkAreaAt(surfaces, X, Math.Min(Y, Y + _vy * dt) - 1);
        double left = wa.Left, right = wa.Right;
        if (_crossTo is { } to)
        {
            left = Math.Min(left, to.Left);
            right = Math.Max(right, to.Right);
        }
        double nx = Math.Clamp(X + _vx * dt, left + petWidth * HalfBody, right - petWidth * HalfBody);
        double ny = Y + _vy * dt;
        Motion = _vy < 0 ? Motion.Jump : Motion.Fall;

        if (_vy >= 0)
        {
            var landing = surfaces.Platforms
                .Where(p => p.Contains(nx) && Y <= p.Y + 0.5 && ny >= p.Y && IsThere(p))
                .OrderBy(p => p.Y)
                .Cast<Platform?>()
                .FirstOrDefault();

            // Below the taskbar (e.g. dropped onto it): step up onto it.
            var floor = surfaces.Platforms.Where(p => p.IsFloor && p.Contains(nx)).Cast<Platform?>().FirstOrDefault();
            if (landing == null && floor is { } f && ny >= f.Y) landing = f;

            if (landing is { } l)
            {
                X = nx;
                Land(l, petHeight);
                return;
            }
        }
        X = nx;
        Y = ny;
    }

    private static double Gravity(double petHeight) => petHeight * 40;

    private double Between(double min, double max) => min + _random.NextDouble() * (max - min);
}
