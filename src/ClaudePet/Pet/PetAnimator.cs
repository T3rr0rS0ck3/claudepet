using ClaudePet.Core;

namespace ClaudePet.Pet;

/// <summary>Things the user can do to the pet from the buttons shown while hovering it.</summary>
public enum Emote { Feed, Pat, Play, Tickle }

/// <summary>Maps mood + animation tick (8 per second) to a sprite frame.</summary>
public static class PetAnimator
{
    public const int TicksPerSecond = 8;

    /// <summary>Length of an emote's reaction in ticks.</summary>
    public static int EmoteTicks(Emote emote) => emote switch
    {
        Emote.Feed => 32,
        Emote.Play => 24,
        _ => 28,
    };

    /// <summary>The pet's reaction to an emote, t ticks after it started.</summary>
    public static SpriteFrame EmoteFrame(Emote emote, long t) => emote switch
    {
        // Looks up at the cookie with the mouth open, munches it, then beams.
        Emote.Feed when t < 8 => new SpriteFrame(Eyes.LookUp, Mouth.Open, Arms.Up, Mark: Mark.Cookie),
        Emote.Feed when t < 22 => new SpriteFrame(Eyes.Closed, t / 2 % 2 == 0 ? Mouth.Small : Mouth.Smile,
            Bob: (int)(t / 2 % 2), Mark: Mark.Crumbs, Blush: true),
        Emote.Feed => new SpriteFrame(Eyes.Happy, Mouth.Smile, t / 3 % 2 == 0 ? Arms.Wave : Arms.Down,
            Mark: Mark.Heart1, Blush: true),

        // Leans into the hand, hearts drifting up.
        Emote.Pat => new SpriteFrame(t % 20 == 10 ? Eyes.Closed : Eyes.Happy, Mouth.Smile,
            Bob: (int)(t / 4 % 2), Shake: (t / 4 % 4) switch { 1 => 1, 3 => -1, _ => 0 },
            Mark: t / 4 % 2 == 0 ? Mark.Heart1 : Mark.Heart2, Blush: true),

        // Juggles a ball from one side to the other; the window adds real hops.
        Emote.Play => new SpriteFrame(t / 2 % 4 == 1 ? Eyes.Happy : Eyes.LookUp, Mouth.Open, Arms.Up,
            Legs: (int)(t / 2 % 2) + 1,
            Mark: (t / 2 % 4) switch { 0 => Mark.Ball1, 2 => Mark.Ball3, _ => Mark.Ball2 }),

        // Giggles and wriggles.
        _ => new SpriteFrame(Eyes.Happy, t % 2 == 0 ? Mouth.Open : Mouth.Smile,
            t / 2 % 2 == 0 ? Arms.Up : Arms.Down, Shake: t % 2 == 0 ? 1 : -1, Legs: (int)(t % 2) + 1, Blush: true),
    };

    public static SpriteFrame Frame(PetMood mood, bool working, bool cheering, long t,
        Motion motion = Motion.Idle, int direction = 1, bool listening = false)
    {
        var frame = MoodFrame(mood, working, cheering, t);
        // Dictating to Claude Code: stands still and talks into the microphone in its hand, sound waves rising
        if (listening && motion is Motion.Idle or Motion.Walk or Motion.Run)
        {
            bool blink = t % 36 == 0;
            var mouth = (t / 2 % 5) switch { 0 or 3 => Mouth.Open, 2 => Mouth.None, _ => Mouth.Small };
            var waves = (t / 3 % 4) switch { 1 => Mark.Listen1, 2 => Mark.Listen2, 3 => Mark.Listen3, _ => Mark.None };
            return frame with
            {
                Eyes = frame.Eyes == Eyes.Closed ? Eyes.Closed : blink ? Eyes.Blink : t / 24 % 3 == 2 ? Eyes.Happy : Eyes.LookRight,
                Mouth = mouth, Arms = Arms.Mic, Legs = 0, Shake = 0,
                Bob = (int)(t / 6 % 2), Mark = waves,
            };
        }
        return motion switch
        {
            Motion.Walk or Motion.Run => Walking(frame, motion == Motion.Run, direction, t),
            Motion.Fall => frame with
            {
                Eyes = Eyes.Big, Mouth = Mouth.Open, Arms = Arms.Up, Legs = (int)(t % 2) + 1,
                Bob = 0, Shake = 0, Mark = Mark.None,
            },
            // Picked up: wide-eyed like falling, legs kicking and wriggling a little
            Motion.Carried => frame with
            {
                Eyes = Eyes.Big, Mouth = Mouth.Open, Arms = Arms.Up, Legs = (int)(t % 2) + 1,
                Bob = 0, Shake = t / 3 % 2 == 0 ? 1 : 0, Mark = Mark.None,
            },
            Motion.Climb => frame with
            {
                Eyes = frame.Eyes == Eyes.Closed ? Eyes.Closed : Eyes.LookUp,
                Arms = t / 2 % 2 == 0 ? Arms.Up : Arms.Wave, Legs = (int)(t / 2 % 2) + 1,
                Bob = 0, Shake = 0,
            },
            Motion.Hang => frame with
            {
                Eyes = frame.Eyes is Eyes.Closed or Eyes.Blink ? frame.Eyes : direction < 0 ? Eyes.LookLeft : Eyes.LookRight,
                Arms = t / 3 % 2 == 0 ? Arms.Up : Arms.Wave, Legs = (int)(t / 3 % 2) + 1,
                Bob = 0, Shake = 0, Mark = Mark.None,
            },
            // Squashed flat after a long fall; the window does the squashing.
            Motion.Splat => frame with
            {
                Eyes = Eyes.Closed, Mouth = Mouth.Wavy, Arms = Arms.Wave, Legs = 0,
                Bob = 0, Shake = 0, Mark = Mark.None,
            },
            Motion.Jump => frame with
            {
                Eyes = Eyes.Happy, Mouth = Mouth.Smile, Arms = Arms.Up, Legs = 0,
                Bob = 0, Shake = 0, Mark = Mark.None,
            },
            _ => frame,
        };
    }

    /// <summary>The mood's frame with stepping legs and eyes looking where it is going.</summary>
    private static SpriteFrame Walking(SpriteFrame frame, bool run, int direction, long t)
    {
        int step = (int)(run ? t % 2 : t / 2 % 2);
        var eyes = frame.Eyes switch
        {
            Eyes.Normal or Eyes.LookLeft or Eyes.LookRight or Eyes.LookUp or Eyes.LookDown =>
                direction < 0 ? Eyes.LookLeft : Eyes.LookRight,
            _ => frame.Eyes, // blinking, happy, wide, ...
        };
        var arms = frame.Arms is Arms.TypeLeft or Arms.TypeRight ? Arms.Down : frame.Arms;
        return frame with { Eyes = eyes, Arms = arms, Legs = step + 1, Bob = step };
    }

    private static SpriteFrame MoodFrame(PetMood mood, bool working, bool cheering, long t)
    {
        bool blink = t % 36 == 0 || t % 108 == 3;
        int breathe = (int)(t / 8 % 2);
        Eyes Open(Eyes eyes) => blink ? Eyes.Blink : eyes;

        if (cheering)
            return new SpriteFrame(Eyes.Happy, Mouth.Smile, t / 2 % 2 == 0 ? Arms.Up : Arms.Down,
                Bob: (int)(t / 2 % 2), Blush: true);

        switch (mood)
        {
            case PetMood.Relaxed:
            case PetMood.Normal:
            {
                if (working) return Working(t, blink);
                long cycle = t % 200;
                if (mood == PetMood.Relaxed && cycle is >= 120 and < 145)
                    return new SpriteFrame(Eyes.Happy, Mouth.Smile, t / 3 % 2 == 0 ? Arms.Wave : Arms.Down,
                        Bob: breathe, Blush: true);
                if (cycle is >= 160 and < 176)
                    return new SpriteFrame(cycle < 168 ? Eyes.LookLeft : Eyes.LookRight, Bob: breathe);
                return new SpriteFrame(Open(Eyes.Normal), Bob: breathe);
            }

            case PetMood.Attentive:
            {
                if (working) return Working(t, blink);
                var dots = (t / 5 % 4) switch { 1 => Mark.Dots1, 2 => Mark.Dots2, 3 => Mark.Dots3, _ => Mark.None };
                return new SpriteFrame(Open(Eyes.LookUp), Bob: (int)(t / 12 % 2), Mark: dots);
            }

            case PetMood.Nervous:
            {
                var eyes = (t / 6 % 4) switch { 0 => Eyes.LookLeft, 2 => Eyes.LookRight, _ => Eyes.Normal };
                int shake = t % 24 < 4 ? (t % 2 == 0 ? 1 : -1) : 0;
                var arms = working ? (t / 2 % 2 == 0 ? Arms.TypeLeft : Arms.TypeRight) : Arms.Down;
                return new SpriteFrame(Open(eyes), Mouth.Small, arms, Bob: breathe, Shake: shake, Sweat: 1);
            }

            case PetMood.Worried:
            {
                int shake = t % 10 < 3 ? (t % 2 == 0 ? 1 : -1) : 0;
                var arms = working ? (t / 2 % 2 == 0 ? Arms.TypeLeft : Arms.TypeRight) : Arms.Down;
                return new SpriteFrame(Open(Eyes.Wide), Mouth.Wavy, arms, Shake: shake, Sweat: 2);
            }

            case PetMood.Panic:
                return new SpriteFrame(Eyes.Big, Mouth.Open,
                    t / 2 % 2 == 0 ? Arms.Up : Arms.Down,
                    Shake: t % 2 == 0 ? 1 : -1,
                    Legs: (int)(t / 2 % 2) + 1,
                    Mark: t / 3 % 2 == 0 ? Mark.Exclaim : Mark.None,
                    Sweat: 2,
                    Tint: Tint.Hot);

            case PetMood.Exhausted:
            {
                var z = (t / 8 % 3) switch { 0 => Mark.Zzz1, 1 => Mark.Zzz2, _ => Mark.Zzz3 };
                return new SpriteFrame(Eyes.Closed, Bob: (int)(t / 12 % 2), Mark: z, Tint: Tint.Pale);
            }

            default: // Unknown: looking around, wondering where the data is
            {
                // No usage data (e.g. only Desktop sessions), but a hook says Claude is busy.
                if (working) return Working(t, blink);
                var dots = (t / 6 % 3) switch { 0 => Mark.Dots1, 1 => Mark.Dots2, _ => Mark.Dots3 };
                return new SpriteFrame(Open(t / 16 % 2 == 0 ? Eyes.LookLeft : Eyes.LookRight), Bob: breathe, Mark: dots);
            }
        }
    }

    private static SpriteFrame Working(long t, bool blink) =>
        new(blink ? Eyes.Blink : Eyes.LookDown, Arms: t / 2 % 2 == 0 ? Arms.TypeLeft : Arms.TypeRight);
}
