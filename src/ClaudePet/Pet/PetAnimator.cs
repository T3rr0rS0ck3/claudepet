using ClaudePet.Core;

namespace ClaudePet.Pet;

/// <summary>Maps mood + animation tick (8 per second) to a sprite frame.</summary>
public static class PetAnimator
{
    public const int TicksPerSecond = 8;

    public static SpriteFrame Frame(PetMood mood, bool working, bool cheering, long t)
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
                var dots = (t / 6 % 3) switch { 0 => Mark.Dots1, 1 => Mark.Dots2, _ => Mark.Dots3 };
                return new SpriteFrame(Open(t / 16 % 2 == 0 ? Eyes.LookLeft : Eyes.LookRight), Bob: breathe, Mark: dots);
            }
        }
    }

    private static SpriteFrame Working(long t, bool blink) =>
        new(blink ? Eyes.Blink : Eyes.LookDown, Arms: t / 2 % 2 == 0 ? Arms.TypeLeft : Arms.TypeRight);
}
