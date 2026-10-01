using GameLogic;

namespace GameTest;

public class BotSettingsTests
{
    [Fact]
    public void BotSettingsDefaultToNoBotsOnMedium()
    {
        var settings = new MatchSettings();

        Assert.Equal(0, settings.BotCount);
        Assert.Equal(BotDifficulty.Medium, settings.BotDifficulty);
        Assert.False(settings.ClearBotsToWin);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(3, 3)]
    [InlineData(99, MatchSettings.MaxBots)]
    [InlineData(int.MinValue, 0)]
    [InlineData(int.MaxValue, MatchSettings.MaxBots)]
    public void SanitizeKeepsTheBotCountInRange(int sent, int expected)
    {
        var sanitized = MatchSettings.Sanitize(new MatchSettings { BotCount = sent });

        Assert.Equal(expected, sanitized.BotCount);
    }

    [Fact]
    public void SanitizeFallsBackToMediumForAnUnknownDifficulty()
    {
        var sanitized = MatchSettings.Sanitize(new MatchSettings { BotDifficulty = (BotDifficulty)42 });

        Assert.Equal(BotDifficulty.Medium, sanitized.BotDifficulty);
    }

    [Fact]
    public void BotCountAndClearTheBotsStayLockedButDifficultyCanChange()
    {
        var current = new MatchSettings { BotCount = 2, ClearBotsToWin = true, BotDifficulty = BotDifficulty.Easy };
        var incoming = new MatchSettings { BotCount = 5, ClearBotsToWin = false, BotDifficulty = BotDifficulty.Hard };

        var result = incoming.WithLockedFrom(current);

        Assert.Equal(2, result.BotCount);
        Assert.True(result.ClearBotsToWin);
        Assert.Equal(BotDifficulty.Hard, result.BotDifficulty);
    }

    [Fact]
    public void EachDifficultyIsHarderThanTheLast()
    {
        var easy = BotProfile.For(BotDifficulty.Easy);
        var medium = BotProfile.For(BotDifficulty.Medium);
        var hard = BotProfile.For(BotDifficulty.Hard);

        Assert.True(easy.ReactionTicks > medium.ReactionTicks && medium.ReactionTicks > hard.ReactionTicks);
        Assert.True(easy.AimErrorDegrees > medium.AimErrorDegrees && medium.AimErrorDegrees > hard.AimErrorDegrees);
        Assert.True(easy.LeadFactor < medium.LeadFactor && medium.LeadFactor < hard.LeadFactor);
        Assert.True(easy.EvadeChance < medium.EvadeChance && medium.EvadeChance < hard.EvadeChance);
    }

    [Fact]
    public void TankStateSaysWhetherATankIsABot()
    {
        var game = TestGames.NewGame();
        game.JoinGame();
        game.JoinGame();
        var botId = game.Tanks.Last().Id;
        game.Tanks = game.Tanks.Select(t => t.Id == botId ? t with { IsBot = true } : t).ToArray();

        var tanks = game.GetGameState().Tanks!.ToArray();

        Assert.False(tanks[0].IsBot);
        Assert.True(tanks[1].IsBot);
    }
}
