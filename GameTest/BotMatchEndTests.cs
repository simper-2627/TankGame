using GameLogic;
using GameLogic.Game;

namespace GameTest;

public class BotMatchEndTests
{
    private static Tank Human(bool eliminated = false) => new() { Eliminated = eliminated, Health = eliminated ? 0 : 3 };
    private static Tank Bot(bool eliminated = false) => new() { IsBot = true, Eliminated = eliminated, Health = eliminated ? 0 : 3 };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoHumansLeftMeansTheBotsWin(bool singlePlayer)
    {
        // Multiplayer needs two humans to have joined before a result is possible
        var result = Combat.DecideResult([Human(eliminated: true), Human(eliminated: true), Bot()], null, singlePlayer);

        Assert.Equal(new MatchResult(true, null, BotsWon: true), result);
    }

    [Fact]
    public void TheLastHumanAndTheLastBotGoingDownTogetherIsADraw()
    {
        var result = Combat.DecideResult([Human(eliminated: true), Bot(eliminated: true)], null, singlePlayer: true);

        Assert.Equal(new MatchResult(true, null), result);
    }

    [Fact]
    public void SinglePlayerIsWonByClearingEveryBot()
    {
        var human = Human();

        var result = Combat.DecideResult([human, Bot(eliminated: true), Bot(eliminated: true)], null, singlePlayer: true);

        Assert.Equal(new MatchResult(true, human.Id), result);
    }

    [Fact]
    public void SinglePlayerKeepsGoingWhileABotLives()
    {
        var result = Combat.DecideResult([Human(), Bot(eliminated: true), Bot()], null, singlePlayer: true);

        Assert.Equal(MatchResult.Ongoing, result);
    }

    [Fact]
    public void SinglePlayerTimeRunningOutWithABotAliveIsABotWin()
    {
        var result = Combat.DecideResult([Human(), Bot()], 0, singlePlayer: true);

        Assert.Equal(new MatchResult(true, null, BotsWon: true), result);
    }

    [Fact]
    public void ClearTheBotsMakesNoDifferenceInSinglePlayer()
    {
        var human = Human();

        Assert.Equal(MatchResult.Ongoing, Combat.DecideResult([human, Bot()], null, singlePlayer: true, clearBotsToWin: true));
        Assert.Equal(new MatchResult(true, human.Id),
            Combat.DecideResult([human, Bot(eliminated: true)], null, singlePlayer: true, clearBotsToWin: true));
    }

    [Fact]
    public void MultiplayerLastHumanWinsEvenWithBotsStillAlive()
    {
        var winner = Human();

        var result = Combat.DecideResult([winner, Human(eliminated: true), Bot(), Bot()], null);

        Assert.Equal(new MatchResult(true, winner.Id), result);
    }

    [Fact]
    public void ClearTheBotsKeepsTheMatchGoingWhileBotsLive()
    {
        var result = Combat.DecideResult([Human(), Human(eliminated: true), Bot()], null, clearBotsToWin: true);

        Assert.Equal(MatchResult.Ongoing, result);
    }

    [Fact]
    public void ClearTheBotsEndsOnceTheBotsAreGone()
    {
        var winner = Human();

        var result = Combat.DecideResult([winner, Human(eliminated: true), Bot(eliminated: true)], null, clearBotsToWin: true);

        Assert.Equal(new MatchResult(true, winner.Id), result);
    }

    [Fact]
    public void ClearTheBotsStillEndsWhenTimeRunsOut()
    {
        var winner = Human();

        var result = Combat.DecideResult([winner, Human(eliminated: true), Bot()], 0, clearBotsToWin: true);

        Assert.Equal(new MatchResult(true, winner.Id), result);
    }

    [Fact]
    public void TwoHumansKeepFightingWhileBotsLive()
    {
        var result = Combat.DecideResult([Human(), Human(), Bot()], null);

        Assert.Equal(MatchResult.Ongoing, result);
    }

    [Fact]
    public void WhenTimeRunsOutOnlyHumansAreRanked()
    {
        var steady = new Tank { Deaths = 1, Health = 3 };
        var shaky = new Tank { Deaths = 2, Health = 3 };
        // The bot has the best numbers in the match but isn't competing
        var bot = new Tank { IsBot = true, Deaths = 0, Health = 3, HitsLanded = 9 };

        var result = Combat.DecideResult([steady, shaky, bot], 0);

        Assert.Equal(new MatchResult(true, steady.Id), result);
    }

    [Fact]
    public void HumansTiedWhenTimeRunsOutIsADrawNotABotWin()
    {
        var result = Combat.DecideResult([new Tank(), new Tank(), Bot()], 0);

        Assert.Equal(new MatchResult(true, null), result);
    }

    [Fact]
    public void AMatchWithOnlyOneHumanNeverEndsUnderMultiplayerRules()
    {
        Assert.Equal(MatchResult.Ongoing, Combat.DecideResult([Human(), Bot()], null));
        Assert.Equal(MatchResult.Ongoing, Combat.DecideResult([Human(eliminated: true), Bot()], null));
        Assert.Equal(MatchResult.Ongoing, Combat.DecideResult([Human(), Bot()], 0));
    }

    [Fact]
    public void TimeUpWithABotAliveAndClearTheBotsOffTheRemainingHumanWins()
    {
        var winner = Human();

        var result = Combat.DecideResult([winner, Human(eliminated: true), Bot()], 0);

        Assert.Equal(new MatchResult(true, winner.Id), result);
    }

    [Fact]
    public async Task TheBotsWinningShowsInTheGameState()
    {
        var game = TestGames.NewGame(matchType: GameMatchTypes.Bots);
        game.Tanks = [Human(eliminated: true), Bot()];

        await game.loopRunner.ProcessGameTick();

        var state = game.GetGameState();
        Assert.Equal(GameStatus.Ended, state.Status);
        Assert.True(state.BotsWon);
        Assert.Null(state.WinnerId);
    }

    [Fact]
    public async Task ClearingTheBotsShowsAsAHumanWin()
    {
        var game = TestGames.NewGame(matchType: GameMatchTypes.Bots);
        var human = Human();
        game.Tanks = [human, Bot(eliminated: true)];

        await game.loopRunner.ProcessGameTick();

        var state = game.GetGameState();
        Assert.Equal(GameStatus.Ended, state.Status);
        Assert.False(state.BotsWon);
        Assert.Equal(human.Id, state.WinnerId);
    }
}
