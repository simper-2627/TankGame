using System.Globalization;

namespace GameLogic;

public enum TankPattern { None, Stripes, Camo, Spots, Checker, Chevron }

// What a player picked on the home page. Colors are names from TankColors.Player; null means "not chosen"
// (the default rule applies). Comes from the client, so the server always stores Sanitize()'s result
public record TankAppearance
{
    public const int MaxTagLength = 4;
    public static readonly TankAppearance Default = new();

    // null = the id-derived color every tank had before customization
    public string? HullColor { get; init; }
    // null = same as the hull
    public string? BarrelColor { get; init; }
    // null = same as the hull
    public string? BulletColor { get; init; }
    public TankPattern Pattern { get; init; } = TankPattern.None;
    // null = Black
    public string? PatternColor { get; init; }
    // null = the normal label color
    public string? NameColor { get; init; }
    // Shown before the name; up to MaxTagLength characters, an emoji counts as one
    public string Tag { get; init; } = "";

    public TankAppearance Sanitize() => new()
    {
        HullColor = TankColors.OrNull(HullColor),
        BarrelColor = TankColors.OrNull(BarrelColor),
        BulletColor = TankColors.OrNull(BulletColor),
        Pattern = Enum.IsDefined(Pattern) ? Pattern : TankPattern.None,
        PatternColor = TankColors.OrNull(PatternColor),
        NameColor = TankColors.OrNull(NameColor),
        Tag = CleanTag(Tag),
    };

    // Control characters (newlines, tabs) would break the name label onto several lines
    private static string CleanTag(string? tag)
    {
        var cleaned = new string((tag ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        var text = new StringInfo(cleaned);
        return text.LengthInTextElements <= MaxTagLength
            ? cleaned
            : text.SubstringByTextElements(0, MaxTagLength).TrimEnd();
    }
}

// The colors a player may pick. The frontend draws the ones without their own sprite with CSS filters (TankPalette)
public static class TankColors
{
    // Kept for bots so a computer tank can be told apart at a glance
    public const string Bot = "Red";

    // In the order the home page lists them
    public static readonly string[] Player =
        ["Beige", "Black", "Blue", "Green", "Yellow", "Orange", "Brown", "Purple", "Pink", "Navy", "Teal", "Lime", "White", "Gray"];

    // Players who don't pick a color keep the one their id gave them before customization existed
    private static readonly string[] idColors = ["Beige", "Black", "Blue", "Green"];

    public static bool IsPlayerColor(string? color) => color is not null && Player.Contains(color);

    public static string? OrNull(string? color) => IsPlayerColor(color) ? color : null;

    public static string FromId(Guid id) => idColors[id.ToByteArray()[0] % idColors.Length];
}
