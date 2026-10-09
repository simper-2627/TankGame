using GameLogic;

namespace GameFrontend;

// How each tank color is drawn. The Kenney art only has five colors, so the rest are CSS filters over the nearest
// sprite. Filters were tuned by eye on /swatches. Which color a tank gets is decided by GameLogic.TankLook
public static class TankPalette
{
    // Sprite/BulletSprite name the image files (tank{Sprite}.png, bullet{BulletSprite}Silver_outline.png).
    // Hex is the plain color used for name labels and patterns
    public sealed record Swatch(string Sprite, string Filter, string BulletSprite, string BulletFilter, string Hex);

    private static readonly Dictionary<string, Swatch> swatches = new()
    {
        ["Beige"] = new("Beige", "none", "Beige", "none", "#c8a165"),
        // The bullet sprites have no black variant, so black tanks fire silver
        ["Black"] = new("Black", "none", "Silver", "none", "#2b2b2b"),
        ["Blue"] = new("Blue", "none", "Blue", "none", "#3b7dd8"),
        ["Green"] = new("Green", "none", "Green", "none", "#4e9a3c"),
        ["Yellow"] = new("Beige", "sepia(1) saturate(6) hue-rotate(8deg) brightness(1.15)", "Yellow", "none", "#e0b400"),
        ["Orange"] = new("Beige", "sepia(1) saturate(7) hue-rotate(-12deg) brightness(1.05)", "Yellow", "hue-rotate(-25deg) saturate(1.5)", "#e07b1a"),
        ["Brown"] = new("Beige", "sepia(1) saturate(2.2) brightness(0.6)", "Beige", "sepia(1) saturate(2.2) brightness(0.6)", "#7a4f24"),
        ["Purple"] = new("Blue", "hue-rotate(60deg)", "Blue", "hue-rotate(60deg)", "#8a4fd0"),
        ["Pink"] = new("Blue", "hue-rotate(110deg) saturate(0.8) brightness(1.25)", "Blue", "hue-rotate(110deg) saturate(0.8) brightness(1.25)", "#e06aa8"),
        ["Navy"] = new("Blue", "brightness(0.55) saturate(1.3)", "Blue", "brightness(0.55) saturate(1.3)", "#1f3270"),
        ["Teal"] = new("Green", "hue-rotate(60deg)", "Green", "hue-rotate(60deg)", "#1e9a8f"),
        ["Lime"] = new("Green", "hue-rotate(-25deg) saturate(1.6) brightness(1.25)", "Green", "hue-rotate(-25deg) saturate(1.6) brightness(1.25)", "#8bd12a"),
        ["White"] = new("Beige", "grayscale(1) brightness(1.6)", "Silver", "brightness(1.4)", "#f2f2f2"),
        ["Gray"] = new("Black", "grayscale(1) brightness(1.9)", "Silver", "none", "#8a8a8a"),
        [TankColors.Bot] = new("Red", "none", "Red", "none", "#c8312c"),
    };

    // A name this build doesn't know draws as Green rather than a broken image
    public static Swatch For(string color) => swatches.GetValueOrDefault(color) ?? swatches["Green"];

    // CSS background for a pattern layer; the caller clips it to the hull with a mask
    public static string PatternBackground(TankPattern pattern, string hex) => pattern switch
    {
        TankPattern.Stripes => $"repeating-linear-gradient(45deg, {hex} 0 6px, transparent 6px 14px)",
        TankPattern.Checker => $"conic-gradient({hex} 25%, transparent 0 50%, {hex} 0 75%, transparent 0) 0 0 / 16px 16px",
        TankPattern.Spots => $"radial-gradient(circle, {hex} 0 4px, transparent 5px) 0 0 / 16px 16px",
        TankPattern.Camo => $"radial-gradient(14px 9px at 20% 30%, {hex} 98%, transparent 100%) no-repeat, "
            + $"radial-gradient(12px 16px at 70% 60%, {hex} 98%, transparent 100%) no-repeat, "
            + $"radial-gradient(18px 8px at 40% 85%, {hex} 98%, transparent 100%) no-repeat, "
            + $"radial-gradient(10px 12px at 85% 15%, {hex} 98%, transparent 100%) no-repeat",
        TankPattern.Chevron => $"linear-gradient(135deg, {hex} 25%, transparent 25%) -10px 0 / 20px 20px, "
            + $"linear-gradient(225deg, {hex} 25%, transparent 25%) -10px 0 / 20px 20px, "
            + $"linear-gradient(315deg, {hex} 25%, transparent 25%) 0 0 / 20px 20px, "
            + $"linear-gradient(45deg, {hex} 25%, transparent 25%) 0 0 / 20px 20px",
        _ => "none",
    };
}
