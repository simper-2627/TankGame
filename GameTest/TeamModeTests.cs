using GameLogic;
using GameLogic.Game;

namespace GameTest;

public class TeamModeTests
{
    private static MatchResult Decide(int? ticksLeft, params Tank[] tanks) =>
        Combat.DecideResult(tanks, ticksLeft, mode: GameMode.TeamElimination);

    [Fact]
    public void LastTeamStandingWins()
    {
        var result = Decide(null,
            new Tank { Team = 1 }, new Tank { Team = 1, Eliminated = true },
            new Tank { Team = 2, Eliminated = true }, new Tank { Team = 2, Eliminated = true });

        Assert.Equal(new MatchResult(true, null, WinningTeam: 1), result);
    }

    [Fact]
    public void BothTeamsWithTanksLeftKeepPlaying()
    {
        Assert.Equal(MatchResult.Ongoing, Decide(null,
            new Tank { Team = 1 }, new Tank { Team = 2 }, new Tank { Team = 2, Eliminated = true }));
    }

    [Fact]
    public void EveryoneEliminatedIsADraw()
    {
        Assert.Equal(new MatchResult(true, null),
            Decide(null, new Tank { Team = 1, Eliminated = true }, new Tank { Team = 2, Eliminated = true }));
    }

    [Fact]
    public void OneTeamAloneNeverEndsTheMatch()
    {
        Assert.Equal(MatchResult.Ongoing, Decide(null, new Tank { Team = 1 }));
        Assert.Equal(MatchResult.Ongoing, Decide(null, new Tank { Team = 1 }, new Tank { Team = 1, Eliminated = true }));
    }

    [Fact]
    public void TimeUpGoesToTheTeamWithMoreTanksLeft()
    {
        var result = Decide(0,
            new Tank { Team = 1 }, new Tank { Team = 1 },
            new Tank { Team = 2 }, new Tank { Team = 2, Eliminated = true });

        Assert.Equal(new MatchResult(true, null, WinningTeam: 1), result);
    }

    [Fact]
    public void TimeUpWithEqualTanksGoesToFewestDeaths()
    {
        var result = Decide(0, new Tank { Team = 1, Deaths = 3 }, new Tank { Team = 2, Deaths = 1 });

        Assert.Equal(new MatchResult(true, null, WinningTeam: 2), result);
    }

    [Fact]
    public void TimeUpWithAFullTieIsADraw()
    {
        Assert.Equal(new MatchResult(true, null), Decide(0, new Tank { Team = 1 }, new Tank { Team = 2 }));
    }

    [Fact]
    public void JoiningPlayersAlternateTeams()
    {
        var game = TestGames.NewGame(new MatchSettings { Mode = GameMode.TeamElimination });

        var teams = Enumerable.Range(0, 4).Select(_ => game.JoinGame())
            .Select(id => game.Tanks.Single(t => t.Id == id).Team).ToArray();

        Assert.Equal([1, 2, 1, 2], teams);
    }

    [Fact]
    public void BotsDoNotDecideATeamMatch()
    {
        // Bots are teamless: blue is the only team left, so blue wins even with a bot still driving around
        var result = Decide(null,
            new Tank { Team = 1 }, new Tank { Team = 2, Eliminated = true }, new Tank { IsBot = true });

        Assert.Equal(new MatchResult(true, null, WinningTeam: 1), result);
    }

    [Fact]
    public void BotsWinWhenEveryTeamIsWipedOut()
    {
        var result = Decide(null,
            new Tank { Team = 1, Eliminated = true }, new Tank { Team = 2, Eliminated = true }, new Tank { IsBot = true });

        Assert.Equal(new MatchResult(true, null, BotsWon: true), result);
    }

    [Fact]
    public void FreeForAllTanksHaveNoTeam()
    {
        var game = TestGames.NewGame(new MatchSettings());

        var id = game.JoinGame();

        Assert.Null(game.Tanks.Single(t => t.Id == id).Team);
    }

    [Fact]
    public async Task WipingOutTheOtherTeamEndsTheMatch()
    {
        var game = TestGames.NewGame(new MatchSettings { Mode = GameMode.TeamElimination, Health = 1, Lives = 1 });
        var red = game.JoinGame();
        var blue = game.JoinGame();

        TestGames.ShootAt(game, red, blue);
        await TestGames.TickUntil(game, () => game.Status == GameStatus.Ended);

        var state = game.GetGameState();
        Assert.Equal(1, state.WinningTeam);
        Assert.Null(state.WinnerId);
        Assert.Equal(2, state.Tanks!.Single(t => t.Id == blue).Team);
    }
}
