namespace GameLogic.Profiles;

public static class ProfileName
{
    public const int MaxLength = 20;

    // Trimmed, 1 to MaxLength characters, no control characters. Not required to be unique
    public static bool TryNormalize(string? raw, out string name)
    {
        name = raw?.Trim() ?? "";
        return name.Length is >= 1 and <= MaxLength && !name.Any(char.IsControl);
    }
}
