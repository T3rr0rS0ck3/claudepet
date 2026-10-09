using System.Windows.Media;

namespace Claudius.Core;

public static class Palette
{
    public static readonly Brush Green = Make(0x6F, 0xBF, 0x73);
    public static readonly Brush Yellow = Make(0xE5, 0xC1, 0x4B);
    public static readonly Brush Orange = Make(0xE8, 0x89, 0x3A);
    public static readonly Brush Red = Make(0xE5, 0x48, 0x4D);
    public static readonly Brush Neutral = Make(0x77, 0x73, 0x6B);

    public static Brush ForPercent(double percent, MoodThresholds t) => UsageState.MoodFor(percent, t) switch
    {
        PetMood.Relaxed or PetMood.Normal => Green,
        PetMood.Attentive => Yellow,
        PetMood.Nervous => Orange,
        _ => Red,
    };

    private static Brush Make(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
