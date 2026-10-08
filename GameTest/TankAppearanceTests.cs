using GameLogic;
using GameLogic.Game;

namespace GameTest;

public class TankAppearanceTests
{
    [Fact]
    public void ValidChoicesSurviveSanitize()
    {
        var look = new TankAppearance
        {
            HullColor = "Purple", BarrelColor = "Black", BulletColor = "Yellow", Pattern = TankPattern.Camo,
            PatternColor = "White", NameColor = "Teal", Tag = "GG",
        };
        Assert.Equal(look, look.Sanitize());
    }

    [Theory]
    [InlineData("Red")]
    [InlineData("blue")]
    [InlineData("#ff0000")]
    [InlineData("")]
    [InlineData("Magenta")]
    public void UnknownOrBotColorsFallBackToDefault(string color)
    {
        var look = new TankAppearance
        {
            HullColor = color, BarrelColor = color, BulletColor = color, PatternColor = color, NameColor = color,
        };
        Assert.Equal(TankAppearance.Default, look.Sanitize());
    }

    [Fact]
    public void UndefinedPatternBecomesNone() =>
        Assert.Equal(TankPattern.None, new TankAppearance { Pattern = (TankPattern)99 }.Sanitize().Pattern);

    [Theory]
    [InlineData("ACE", "ACE")]
    [InlineData("  ACE  ", "ACE")]
    [InlineData("TOOLONG", "TOOL")]
    [InlineData("🔥🔥🔥🔥🔥", "🔥🔥🔥🔥")]
    [InlineData("👨‍👩‍👧x", "👨‍👩‍👧x")]
    [InlineData("A\nB\tC", "ABC")]
    [InlineData("   ", "")]
    public void TagIsCleanedAndCutToFourCharacters(string tag, string expected) =>
        Assert.Equal(expected, new TankAppearance { Tag = tag }.Sanitize().Tag);

    [Fact]
    public void NullTagFromTheWireBecomesEmpty() =>
        Assert.Equal("", new TankAppearance { Tag = null! }.Sanitize().Tag);

    [Fact]
    public void FourteenPlayerColorsAndBotRedIsNotOne()
    {
        Assert.Equal(14, TankColors.Player.Length);
        Assert.All(TankColors.Player, color => Assert.True(TankColors.IsPlayerColor(color)));
        Assert.False(TankColors.IsPlayerColor(TankColors.Bot));
    }

    [Fact]
    public void IdColorIsTheSameRuleAsBeforeCustomization()
    {
        string[] old = ["Beige", "Black", "Blue", "Green"];
        for (var i = 0; i < 50; i++)
        {
            var id = Guid.NewGuid();
            Assert.Equal(old[id.ToByteArray()[0] % old.Length], TankColors.FromId(id));
        }
    }
}

public class TankAppearanceJoinTests
{
    private static readonly TankAppearance purple = new() { HullColor = "Purple", Pattern = TankPattern.Stripes, Tag = "GG" };

    private static TankState Seen(Game game, Guid tank, Guid? viewer = null) =>
        game.GetGameState(viewerId: viewer).Tanks!.Single(t => t.Id == tank);

    [Fact]
    public void ChosenLookIsSentToEveryViewer()
    {
        var game = TestGames.NewGame();
        var me = game.JoinGame("Ben", purple);
        var other = game.JoinGame();

        Assert.Equal(purple, Seen(game, me, other).Appearance);
    }

    [Fact]
    public void BadLookIsCleanedBeforeAnyoneSeesIt()
    {
        var game = TestGames.NewGame();
        var me = game.JoinGame(null, new TankAppearance { HullColor = "Red", BulletColor = "Purple", Tag = "TOOLONG" });

        Assert.Equal(new TankAppearance { BulletColor = "Purple", Tag = "TOOL" }, Seen(game, me).Appearance);
    }

    [Fact]
    public void NoLookSendsNothingExtra()
    {
        var game = TestGames.NewGame();
        var me = game.JoinGame();

        Assert.Null(Seen(game, me).Appearance);
    }

    [Fact]
    public void LookThatCleansToDefaultSendsNothingExtra()
    {
        var game = TestGames.NewGame();
        var me = game.JoinGame(null, new TankAppearance { HullColor = "Red", Tag = "   " });

        Assert.Null(Seen(game, me).Appearance);
    }

    [Fact]
    public void BotsNeverCarryALook()
    {
        var game = TestGames.NewGame(new MatchSettings { BotCount = 2 }, matchType: GameMatchTypes.Bots);
        game.JoinGame(null, purple);

        var bots = game.GetGameState().Tanks!.Where(t => t.IsBot).ToArray();
        Assert.Equal(2, bots.Length);
        Assert.All(bots, bot => Assert.Null(bot.Appearance));
    }

    [Fact]
    public void TeamMatchStillSendsTheLookSoPatternAndTagCanBeDrawn()
    {
        var game = TestGames.NewGame(new MatchSettings { Mode = GameMode.TeamElimination });
        var me = game.JoinGame(null, purple);

        var seen = Seen(game, me);
        Assert.NotNull(seen.Team);
        Assert.Equal(purple, seen.Appearance);
    }
}
