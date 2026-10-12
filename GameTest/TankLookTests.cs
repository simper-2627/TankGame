using GameLogic;

namespace GameTest;

public class TankLookTests
{
    // First byte 0, so the id rule gives Beige
    private static readonly Guid id = Guid.Empty;

    private static readonly TankAppearance full = new()
    {
        HullColor = "Purple", BarrelColor = "Black", BulletColor = "Yellow", Pattern = TankPattern.Spots,
        PatternColor = "White", NameColor = "Teal", Tag = "GG",
    };

    [Fact]
    public void UncustomizedTankLooksLikeBefore() =>
        Assert.Equal(new ResolvedLook("Beige", "Beige", "Beige", TankPattern.None, "Black", null, ""),
            TankLook.Resolve(id, isBot: false, team: null, appearance: null));

    [Fact]
    public void ChosenColorsApplyOutsideTeams() =>
        Assert.Equal(new ResolvedLook("Purple", "Black", "Yellow", TankPattern.Spots, "White", "Teal", "GG"),
            TankLook.Resolve(id, isBot: false, team: null, full));

    [Fact]
    public void BarrelAndBulletFollowTheHullWhenNotChosen() =>
        Assert.Equal(new ResolvedLook("Navy", "Navy", "Navy", TankPattern.None, "Black", null, ""),
            TankLook.Resolve(id, isBot: false, team: null, new TankAppearance { HullColor = "Navy" }));

    [Theory]
    [InlineData(1, "Blue")]
    [InlineData(2, "Green")]
    public void TeamColorReplacesHullBarrelAndBulletButKeepsPatternNameColorAndTag(int team, string color) =>
        Assert.Equal(new ResolvedLook(color, color, color, TankPattern.Spots, "White", "Teal", "GG"),
            TankLook.Resolve(id, isBot: false, team, full));

    [Fact]
    public void BotsAreAlwaysPlainRed() =>
        Assert.Equal(new ResolvedLook("Red", "Red", "Red", TankPattern.None, "Black", null, ""),
            TankLook.Resolve(id, isBot: true, team: null, full));

    [Fact]
    public void UnknownColorsFromTheWireFallBackToTheDefault() =>
        Assert.Equal(new ResolvedLook("Beige", "Beige", "Beige", TankPattern.None, "Black", null, ""),
            TankLook.Resolve(id, isBot: false, team: null,
                new TankAppearance { HullColor = "Magenta", BulletColor = "Red", NameColor = "red", Pattern = (TankPattern)42 }));

    [Fact]
    public void ResolvesStraightFromATankState()
    {
        var tank = new TankState { Id = id, Team = 1, Appearance = full };
        Assert.Equal(TankLook.Resolve(id, isBot: false, team: 1, full), TankLook.Resolve(tank));
    }
}
