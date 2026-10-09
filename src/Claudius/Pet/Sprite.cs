using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Claudius.Pet;

public enum Eyes { Normal, Blink, Closed, Happy, LookLeft, LookRight, LookUp, LookDown, Wide, Big }
public enum Mouth { None, Smile, Small, Wavy, Open }
public enum Arms { Down, Up, Wave, TypeLeft, TypeRight, Mic }
public enum Mark
{
    None, Dots1, Dots2, Dots3, Exclaim, Zzz1, Zzz2, Zzz3, Listen1, Listen2, Listen3, Question,
    Heart1, Heart2, Cookie, Crumbs, Ball1, Ball2, Ball3,
}
public enum Tint { Normal, Hot, Pale, Ghost }
/// <summary>Something the pet wears, drawn over the body: so far one per AI model (Opus, Sonnet, Haiku).</summary>
public enum Outfit { None, Crown, Sunglasses, Flower, WizardHat }
/// <summary>Headwear for the season or the night; it replaces the outfit's headwear (not its sunglasses).</summary>
public enum Accessory { None, WitchHat, SantaHat, PartyHat, BunnyEars, Nightcap }

/// <summary>Everything that describes one rendered frame of the pet.</summary>
public readonly record struct SpriteFrame(
    Eyes Eyes,
    Mouth Mouth = Mouth.None,
    Arms Arms = Arms.Down,
    int Bob = 0,
    int Shake = 0,
    int Legs = 0,
    Mark Mark = Mark.None,
    int Sweat = 0,
    bool Blush = false,
    Tint Tint = Tint.Normal,
    Outfit Outfit = Outfit.None,
    Accessory Accessory = Accessory.None);

/// <summary>
/// Procedural pixel-art renderer for the pet: a chunky orange block
/// with two dark eyes, little side arms and four stubby legs.
/// </summary>
public static class Sprite
{
    public const int Width = 22;
    public const int Height = 16;

    private const uint EyeColor = 0xFF1F1A17;
    private const uint White = 0xFFFFFFFF;
    private const uint SweatColor = 0xFF67B7F0;
    private const uint BlushColor = 0xFFF2A28C;
    private const uint MarkColor = 0xFF8C90B8;
    private const uint AlertColor = 0xFFE5484D;
    private const uint QuestionColor = 0xFFE5C14B;
    private const uint HeartColor = 0xFFEF5D7A;
    private const uint CookieColor = 0xFFC98D4F;
    private const uint ChipColor = 0xFF5B3A22;
    private const uint BallColor = 0xFF5B9BE0;
    private const uint MicColor = 0xFFC9CAD4;
    private const uint HandleColor = 0xFF3A3A42;
    private const uint GoldColor = 0xFFF2C14E;
    private const uint GoldShade = 0xFFC48A2C;
    private const uint JewelColor = 0xFFE5484D;
    private const uint LensColor = 0xFF22232B;
    private const uint GlintColor = 0xFF8FA6CC;
    private const uint PetalColor = 0xFFFFFFFF;
    private const uint LeafColor = 0xFF5FAE4E;
    private const uint HatColor = 0xFF7B5CC4;
    private const uint HatShade = 0xFF5A3F9A;
    private const uint WitchColor = 0xFF3B2A4F;
    private const uint BandOrange = 0xFFE8862E;
    private const uint StickColor = 0xFF7A5230;
    private const uint StrawColor = 0xFFD9B45A;
    private const uint BindingColor = 0xFF8C3A2E;
    private const uint SantaRed = 0xFFD8343C;
    private const uint FurWhite = 0xFFF4F1EA;
    private const uint EarPink = 0xFFF2A2B4;
    private const uint NightcapBlue = 0xFF4F6FC4;
    private const uint NightcapShade = 0xFF3A55A0;

    /// <summary>Warm orange, the default body color.</summary>
    public const uint DefaultBodyColor = 0xFFD97757;

    private static readonly Dictionary<SpriteFrame, BitmapSource> Cache = new();
    private static uint _bodyColor;
    private static (uint Body, uint Shade) _normal, _hot, _pale;

    static Sprite() => SetBodyColor(DefaultBodyColor);

    /// <summary>Incremented whenever the body color changes, so other caches (tray icons) can refresh.</summary>
    public static int Version { get; private set; }

    /// <summary>Sets the pet's body color (ARGB); shade and the hot/pale mood tints are derived from it.</summary>
    public static void SetBodyColor(uint argb)
    {
        argb |= 0xFF000000;
        if (argb == _bodyColor) return;
        _bodyColor = argb;
        var (h, s, l) = ToHsl(argb);
        // Offsets measured from the original hand-picked orange palette.
        (uint, uint) Pair(double hue, double sat, double light) =>
            (FromHsl(hue, sat, light), FromHsl(hue, sat * 0.76, light - 0.114));
        _normal = Pair(h, s, l);
        _hot = Pair(h - 3, Math.Min(1, s + 0.14), l - 0.025);
        _pale = Pair(h + 2, s * 0.67, Math.Min(0.9, l + 0.037));
        Cache.Clear();
        Version++;
    }

    /// <summary>
    /// The outfit for an AI model, by name ("Opus 4.1") or id ("…-sonnet-4-5"): Opus wears a crown,
    /// Sonnet sunglasses, Haiku a flower, Fable a wizard's hat; unknown models nothing.
    /// </summary>
    public static Outfit OutfitFor(string? model) =>
        model == null ? Outfit.None
        : model.Contains("opus", StringComparison.OrdinalIgnoreCase) ? Outfit.Crown
        : model.Contains("sonnet", StringComparison.OrdinalIgnoreCase) ? Outfit.Sunglasses
        : model.Contains("haiku", StringComparison.OrdinalIgnoreCase) ? Outfit.Flower
        : model.Contains("fable", StringComparison.OrdinalIgnoreCase) ? Outfit.WizardHat
        : Outfit.None;

    public static BitmapSource Render(SpriteFrame frame)
    {
        if (Cache.TryGetValue(frame, out var cached)) return cached;
        var bitmap = new WriteableBitmap(Width, Height, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, Width, Height), Pixels(frame), Width * 4, 0);
        bitmap.Freeze();
        Cache[frame] = bitmap;
        return bitmap;
    }

    /// <summary>BGRA pixels, row-major, Width x Height.</summary>
    public static uint[] Pixels(SpriteFrame f)
    {
        var px = new uint[Width * Height];
        void Set(int x, int y, uint c)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height) px[y * Width + x] = c;
        }

        (uint body, uint shade) = f.Tint switch
        {
            Tint.Hot => _hot,
            Tint.Pale => _pale,
            Tint.Ghost => (0xFFE4ECF8u, 0xFFB3C2DBu),
            _ => _normal,
        };

        int bx = 2 + f.Shake;   // left edge incl. arms
        int by = 5 + f.Bob;     // top edge of the body
        int c0 = bx + 2;        // left edge of the body (14 px wide, 9 px tall)
        int Mirror(int x) => 2 * bx + 17 - x;

        // Body
        for (int y = by; y <= by + 8; y++)
            for (int x = c0; x <= c0 + 13; x++)
                Set(x, y, y == by + 8 ? shade : body);

        // Legs (1/3 left, 10/12 right); Legs = 1 or 2 lifts alternating pairs
        int[] legCols = [1, 3, 10, 12];
        for (int i = 0; i < legCols.Length; i++)
        {
            bool lifted = f.Legs != 0 && (i % 2 == 0) == (f.Legs == 1);
            for (int y = by + 9; y <= Height - 1 - (lifted ? 1 : 0); y++)
                Set(c0 + legCols[i], y, shade);
        }

        // Arms
        void ArmDown(bool left, int low)
        {
            for (int y = by + 4 + low; y <= by + 5 + low; y++)
                for (int x = bx; x <= bx + 1; x++)
                    Set(left ? x : Mirror(x), y, body);
        }
        void ArmUp(bool left)
        {
            (int x, int y)[] pts = [(bx + 1, by + 3), (bx + 1, by + 2), (bx, by + 2), (bx, by + 1)];
            foreach (var (x, y) in pts) Set(left ? x : Mirror(x), y, body);
        }
        switch (f.Arms)
        {
            case Arms.Up: ArmUp(true); ArmUp(false); break;
            case Arms.Wave: ArmUp(true); ArmDown(false, 0); break;
            case Arms.TypeLeft: ArmDown(true, 1); ArmDown(false, 0); break;
            case Arms.TypeRight: ArmDown(true, 0); ArmDown(false, 1); break;
            default: ArmDown(true, 0); ArmDown(false, 0); break;
        }

        // Eyes: o = direction towards the outside of the face
        void Eye(int ex, int o)
        {
            switch (f.Eyes)
            {
                case Eyes.Blink:
                    Set(ex, by + 3, EyeColor); Set(ex + o, by + 3, EyeColor); break;
                case Eyes.Closed:
                    Set(ex - o, by + 3, EyeColor); Set(ex, by + 3, EyeColor); Set(ex + o, by + 3, EyeColor); break;
                case Eyes.Happy:
                    Set(ex + o, by + 3, EyeColor); Set(ex, by + 2, EyeColor); Set(ex - o, by + 3, EyeColor); break;
                case Eyes.LookLeft:
                    Set(ex - 1, by + 2, EyeColor); Set(ex - 1, by + 3, EyeColor); break;
                case Eyes.LookRight:
                    Set(ex + 1, by + 2, EyeColor); Set(ex + 1, by + 3, EyeColor); break;
                case Eyes.LookUp:
                    Set(ex, by + 1, EyeColor); Set(ex, by + 2, EyeColor); break;
                case Eyes.LookDown:
                    Set(ex, by + 3, EyeColor); Set(ex, by + 4, EyeColor); break;
                case Eyes.Wide:
                    for (int y = by + 2; y <= by + 3; y++) { Set(ex, y, EyeColor); Set(ex - o, y, EyeColor); }
                    Set(ex, by + 2, White);
                    break;
                case Eyes.Big:
                    for (int y = by + 1; y <= by + 3; y++) { Set(ex, y, EyeColor); Set(ex - o, y, EyeColor); }
                    Set(ex, by + 1, White);
                    break;
                default:
                    Set(ex, by + 2, EyeColor); Set(ex, by + 3, EyeColor); break;
            }
        }
        // Behind sunglasses the eyes stay hidden, even where looking up or down would peek past the lenses
        if (f.Outfit != Outfit.Sunglasses)
        {
            Eye(c0 + 3, -1);
            Eye(c0 + 10, 1);
        }

        // Mouth
        switch (f.Mouth)
        {
            case Mouth.Smile:
                Set(c0 + 5, by + 5, EyeColor); Set(c0 + 6, by + 6, EyeColor);
                Set(c0 + 7, by + 6, EyeColor); Set(c0 + 8, by + 5, EyeColor);
                break;
            case Mouth.Small:
                Set(c0 + 6, by + 6, EyeColor); Set(c0 + 7, by + 6, EyeColor);
                break;
            case Mouth.Wavy:
                Set(c0 + 5, by + 6, EyeColor); Set(c0 + 6, by + 5, EyeColor);
                Set(c0 + 7, by + 6, EyeColor); Set(c0 + 8, by + 5, EyeColor);
                break;
            case Mouth.Open:
                for (int y = by + 5; y <= by + 6; y++) { Set(c0 + 6, y, EyeColor); Set(c0 + 7, y, EyeColor); }
                break;
        }

        // Microphone held upright in the right hand, next to the face: a silver head with a dark grille
        if (f.Arms == Arms.Mic)
        {
            Glyph(c0 + 15, MicColor, by + 1, "XXX", "XXX");
            Glyph(c0 + 15, HandleColor, by + 1, ".X.", "X.X");
            for (int y = by + 3; y <= by + 6; y++) Set(c0 + 16, y, HandleColor);
        }

        if (f.Blush)
        {
            Set(c0 + 1, by + 4, BlushColor); Set(c0 + 2, by + 4, BlushColor);
            Set(c0 + 11, by + 4, BlushColor); Set(c0 + 12, by + 4, BlushColor);
        }

        // Outfit; headwear sits on the left half of the head, out of the way of the marks above its middle and right.
        // A seasonal or night accessory takes the head, so only the sunglasses stay with it.
        var outfit = f.Accessory != Accessory.None && f.Outfit != Outfit.Sunglasses ? Outfit.None : f.Outfit;
        switch (outfit)
        {
            // Opus: a golden crown with a red jewel, sitting on the head
            case Outfit.Crown:
                Glyph(c0, GoldColor, by - 3, "X.X.X", "XXXXX", "XXXXX");
                Glyph(c0, GoldShade, by - 3, ".....", ".....", "XXXXX");
                Set(c0 + 2, by - 2, JewelColor);
                break;
            // Sonnet: dark sunglasses over both eyes, with a glint on each lens
            case Outfit.Sunglasses:
                Glyph(c0, LensColor, by + 1, "XXXXXXXXXXXXXX", ".XXXXX..XXXXX.", "..XXX....XXX..");
                Set(c0 + 2, by + 2, GlintColor);
                Set(c0 + 9, by + 2, GlintColor);
                break;
            // Haiku: a little daisy tucked onto the head
            case Outfit.Flower:
                Glyph(c0 + 1, PetalColor, by - 3, ".X.", "X.X", ".X.");
                Set(c0 + 2, by - 2, GoldColor);
                Set(c0 + 3, by - 1, LeafColor);
                break;
            // Fable: a pointed wizard's hat with a little star, its tip bent to the side
            case Outfit.WizardHat:
                Glyph(c0 - 1, HatColor, by - 5, "....X.", "...XX.", "..XXX.", ".XXXX.");
                Glyph(c0 - 1, HatShade, by - 1, "XXXXXX");
                Set(c0 + 2, by - 3, GoldColor);
                break;
        }

        // Accessory, in the same place as the outfit's headwear
        switch (f.Accessory)
        {
            // Halloween: a crooked witch's hat with an orange band, and a broom in the right hand
            // (put away while the hand holds the microphone)
            case Accessory.WitchHat:
                Glyph(c0 - 2, WitchColor, by - 5, "....X...", "...XX...", "..XXX...", "..XXXX..", "XXXXXXXX");
                Glyph(c0 - 2, BandOrange, by - 2, "..XXXX..");
                if (f.Arms != Arms.Mic)
                {
                    for (int y = by + 1; y <= by + 6; y++) Set(c0 + 16, y, StickColor);
                    Glyph(c0 + 15, BindingColor, by + 7, "XXX");
                    Glyph(c0 + 15, StrawColor, by + 8, "XXX", "XXX", "X.X");
                }
                break;
            // Christmas: a red cap with a white brim, its tip and bobble flopping to the left
            case Accessory.SantaHat:
                Glyph(c0 - 2, SantaRed, by - 4, ".XX....", ".XXXX..", ".XXXXX.");
                Glyph(c0 - 2, FurWhite, by - 4, "X......", ".......", ".......", "XXXXXXX");
                break;
            // New Year's Eve and birthdays: a striped party cone leaning to the left, a golden pompom on its tip
            case Accessory.PartyHat:
                Glyph(c0 - 2, GoldColor, by - 5, "X");
                Glyph(c0 - 2, HeartColor, by - 4, ".X.....", "..X.X..", "..X.X.X", "....X.X");
                Glyph(c0 - 2, BallColor, by - 4, "..X....", ".X.X...", "...X.X.", "...X.X.");
                break;
            // Easter: two bunny ears with pink insides
            case Accessory.BunnyEars:
                Glyph(c0, FurWhite, by - 5, "XX.XX", "X..X.", "X..X.", "X..X.", "XX.XX");
                Glyph(c0, EarPink, by - 4, ".X..X", ".X..X", ".X..X");
                break;
            // Night: a long blue nightcap, its tip hanging down beside the head with a white bobble
            case Accessory.Nightcap:
                Glyph(c0 - 2, NightcapBlue, by - 4, "..XXX..", ".XXXXX.", "X.XXXXX");
                Glyph(c0 - 2, NightcapShade, by - 1, "X.XXXXX"); // the gap keeps the hanging tip apart
                Set(c0 - 2, by, FurWhite);
                break;
        }

        // Sweat drops next to the head
        if (f.Sweat >= 1) { Set(c0 + 14, by - 2, SweatColor); Set(c0 + 14, by - 1, SweatColor); }
        if (f.Sweat >= 2) { Set(c0 - 1, by - 2, SweatColor); Set(c0 - 1, by - 1, SweatColor); }

        // Marks above the head
        void Z(int x, int y)
        {
            for (int i = 0; i < 5; i++) { Set(x + i, y, MarkColor); Set(x + i, y + 4, MarkColor); }
            Set(x + 3, y + 1, MarkColor);
            Set(x + 2, y + 2, MarkColor);
            Set(x + 1, y + 3, MarkColor);
        }
        switch (f.Mark)
        {
            case Mark.Dots1 or Mark.Dots2 or Mark.Dots3:
                int count = f.Mark - Mark.Dots1 + 1;
                (int x, int y)[] dots = [(c0 + 10, 3), (c0 + 12, 2), (c0 + 14, 1)];
                for (int i = 0; i < count; i++) Set(dots[i].x, dots[i].y, MarkColor);
                break;
            case Mark.Exclaim:
                for (int y = 0; y <= 1; y++) { Set(c0 + 6, y, AlertColor); Set(c0 + 7, y, AlertColor); }
                Set(c0 + 6, 3, AlertColor); Set(c0 + 7, 3, AlertColor);
                break;
            // One Z drifting away from the head, then a short pause
            case Mark.Zzz1: Z(c0 + 11, 0); break;
            case Mark.Zzz2: Z(c0 + 13, 0); break;
            case Mark.Zzz3: break;
            // The assistant sessions: a question is waiting
            case Mark.Question:
                Glyph(c0 + 5, QuestionColor, 0, "XXX", "..X", ".XX", "...", ".X.");
                break;
            // Emotes: hearts drifting up, a cookie on its way into the mouth, crumbs on the chin, a juggled ball
            case Mark.Heart1: Glyph(c0 + 9, HeartColor, 1, "XX.XX", "XXXXX", ".XXX.", "..X.."); break;
            case Mark.Heart2: Glyph(c0 + 11, HeartColor, 0, "XX.XX", "XXXXX", ".XXX.", "..X.."); break;
            case Mark.Cookie:
                Glyph(c0 + 5, CookieColor, 0, ".XXX.", "XXXXX", "XXXXX", ".XXX.");
                Glyph(c0 + 5, ChipColor, 0, ".....", ".X...", "...X.", ".....");
                break;
            case Mark.Crumbs:
                Set(c0 + 5, by + 7, CookieColor); Set(c0 + 9, by + 7, CookieColor); Set(c0 + 7, by + 8, CookieColor);
                break;
            case Mark.Ball1: Glyph(c0 + 2, BallColor, 1, ".XX.", "XXXX", "XXXX", ".XX."); break;
            case Mark.Ball2: Glyph(c0 + 5, BallColor, 0, ".XX.", "XXXX", "XXXX", ".XX."); break;
            case Mark.Ball3: Glyph(c0 + 8, BallColor, 1, ".XX.", "XXXX", "XXXX", ".XX."); break;
            // Sound waves next to the head, growing outwards: the pet is listening
            case Mark.Listen1 or Mark.Listen2 or Mark.Listen3:
                int waves = f.Mark - Mark.Listen1 + 1;
                Arc(c0 + 11, 1, 3);
                if (waves >= 2) Arc(c0 + 13, 0, 5);
                if (waves >= 3) Arc(c0 + 16, 0, 5);
                break;
        }

        // Rows of a small pixel glyph above the head, starting at row top, X = pixel
        void Glyph(int x, uint color, int top, params string[] rows)
        {
            for (int y = 0; y < rows.Length; y++)
                for (int i = 0; i < rows[y].Length; i++)
                    if (rows[y][i] == 'X') Set(x + i, top + y, color);
        }

        // ")" shaped arc, h pixels tall
        void Arc(int x, int top, int h)
        {
            Set(x, top, MarkColor);
            for (int y = top + 1; y < top + h - 1; y++) Set(x + 1, y, MarkColor);
            Set(x, top + h - 1, MarkColor);
        }

        return px;
    }

    /// <summary>Small tray icon showing the pet for the given frame.</summary>
    public static System.Drawing.Icon CreateIcon(SpriteFrame frame)
    {
        // Crop to the creature itself (arms + body + legs) and scale up with hard edges.
        const int cropX = 2, cropY = 4, cropW = 18, cropH = 12;
        var pixels = Pixels(frame);
        using var source = new System.Drawing.Bitmap(cropW, cropH, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        for (int y = 0; y < cropH; y++)
            for (int x = 0; x < cropW; x++)
                source.SetPixel(x, y, System.Drawing.Color.FromArgb(unchecked((int)pixels[(y + cropY) * Width + x + cropX])));

        using var target = new System.Drawing.Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = System.Drawing.Graphics.FromImage(target))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            int h = 32 * cropH / cropW;
            g.DrawImage(source, new System.Drawing.Rectangle(0, (32 - h) / 2, 32, h));
        }
        return System.Drawing.Icon.FromHandle(target.GetHicon());
    }

    // ---------------------------------------------------------------- colors

    /// <summary>Parses "#RRGGBB" (or "RRGGBB") into an opaque ARGB value.</summary>
    public static bool TryParseColor(string? text, out uint argb)
    {
        argb = 0;
        string hex = text?.Trim().TrimStart('#') ?? "";
        if (hex.Length != 6 || !uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out uint rgb)) return false;
        argb = 0xFF000000 | rgb;
        return true;
    }

    public static string ToHex(uint argb) => $"#{argb & 0xFFFFFF:X6}";

    private static (double H, double S, double L) ToHsl(uint argb)
    {
        double r = (argb >> 16 & 0xFF) / 255.0, g = (argb >> 8 & 0xFF) / 255.0, b = (argb & 0xFF) / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double l = (max + min) / 2, d = max - min;
        if (d == 0) return (0, 0, l);
        double s = d / (1 - Math.Abs(2 * l - 1));
        double h = max == r ? (g - b) / d % 6 : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
        return (h * 60, s, l);
    }

    private static uint FromHsl(double h, double s, double l)
    {
        h = (h % 360 + 360) % 360;
        s = Math.Clamp(s, 0, 1);
        l = Math.Clamp(l, 0, 1);
        double c = (1 - Math.Abs(2 * l - 1)) * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = l - c / 2;
        (double r, double g, double b) = (int)(h / 60) switch
        {
            0 => (c, x, 0d),
            1 => (x, c, 0d),
            2 => (0d, c, x),
            3 => (0d, x, c),
            4 => (x, 0d, c),
            _ => (c, 0d, x),
        };
        uint Byte(double v) => (uint)Math.Round(Math.Clamp(v + m, 0, 1) * 255);
        return 0xFF000000 | Byte(r) << 16 | Byte(g) << 8 | Byte(b);
    }
}
