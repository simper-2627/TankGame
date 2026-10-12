namespace GameLogic;

// The colors, pattern and tag a tank is actually drawn with. Colors are names from TankColors
public record ResolvedLook(string Hull, string Barrel, string Bullet, TankPattern Pattern, string PatternColor, string? NameColor, string Tag);

// Lives here rather than in the frontend so GameTest can cover the rules
public static class TankLook
{
    private const string defaultPatternColor = "Black";

    public static ResolvedLook Resolve(TankState tank) => Resolve(tank.Id, tank.IsBot, tank.Team, tank.Appearance);

    public static ResolvedLook Resolve(Guid id, bool isBot, int? team, TankAppearance? appearance)
    {
        // Bots stay plain red so they're never mistaken for a player
        if (isBot)
            return new(TankColors.Bot, TankColors.Bot, TankColors.Bot, TankPattern.None, defaultPatternColor, null, "");

        // Sanitized again: the server already did, but a client on a different version may not know a color
        var chosen = (appearance ?? TankAppearance.Default).Sanitize();
        var patternColor = chosen.PatternColor ?? defaultPatternColor;

        // In a team match the team color wins so sides read at a glance; the pattern, name color and tag stay personal
        if (team is int t)
        {
            var teamColor = Teams.Name(t);
            return new(teamColor, teamColor, teamColor, chosen.Pattern, patternColor, chosen.NameColor, chosen.Tag);
        }

        var hull = chosen.HullColor ?? TankColors.FromId(id);
        return new(hull, chosen.BarrelColor ?? hull, chosen.BulletColor ?? hull, chosen.Pattern, patternColor, chosen.NameColor, chosen.Tag);
    }
}
