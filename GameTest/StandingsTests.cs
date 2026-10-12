using GameLogic;
using GameLogic.Game;
using GameLogic.Profiles;

namespace GameTest;

public class StandingsTests
{
    private static StandingState[] Build(Tank[] tanks, MatchResult result, Func<Tank, Earnings?>? earned = null) =>
        Standings.Build(tanks, result, earned ?? (_ => null));

    [Fact]
    public void OnlyTheTopThreeAreListed()
    {
        var tanks = Enumerable.Range(0, 5).Select(i => new Tank { Kills = i }).ToArray();

        var top = Build(tanks, new MatchResult(true, null));

        Assert.Equal(3, top.Length);
        Assert.Equal([4, 3, 2], top.Select(entry => entry.Kills));
    }

    [Fact]
    public void TheWinnerIsFirstEvenWithFewerKills()
    {
        var winner = new Tank { Kills = 0 };
        var killer = new Tank { Kills = 5 };

        var top = Build([killer, winner], new MatchResult(true, winner.Id));

        Assert.Equal(winner.Id, top[0].TankId);
        Assert.True(top[0].Won);
        Assert.False(top[1].Won);
    }

    [Fact]
    public void KillsThenHitsDecideTheRestOfTheOrder()
    {
        var most = new Tank { Kills = 2, HitsLanded = 2 };
        var sharper = new Tank { Kills = 1, HitsLanded = 6 };
        var duller = new Tank { Kills = 1, HitsLanded = 3 };

        var top = Build([duller, sharper, most], new MatchResult(true, null));

        Assert.Equal([most.Id, sharper.Id, duller.Id], top.Select(entry => entry.TankId));
    }

    [Fact]
    public void ADrawHasNoWinners()
    {
        var top = Build([new Tank(), new Tank()], new MatchResult(true, null));

        Assert.All(top, entry => Assert.False(entry.Won));
    }

    [Fact]
    public void AWinningTeamMarksEveryoneOnIt()
    {
        var blue = new Tank { Team = 1 };
        var blueToo = new Tank { Team = 1 };
        var green = new Tank { Team = 2, Kills = 9 };

        var top = Build([green, blue, blueToo], new MatchResult(true, null, WinningTeam: 1));

        Assert.Equal([true, true, false], top.Select(entry => entry.Won));
        Assert.Equal(green.Id, top[2].TankId);
    }

    [Fact]
    public void WhenTheBotsWinTheBotsAreTheWinners()
    {
        var human = new Tank { Kills = 3 };
        var bot = new Tank { IsBot = true };

        var top = Build([human, bot], new MatchResult(true, null, BotsWon: true));

        Assert.Equal(bot.Id, top[0].TankId);
        Assert.True(top[0].Won);
        Assert.False(top[1].Won);
    }

    [Fact]
    public void CashComesFromWhatTheTankEarnedAndIsNullWhenItEarnsNothing()
    {
        var paid = new Tank { Kills = 2 };
        var unpaid = new Tank { Kills = 1 };

        var top = Build([paid, unpaid], new MatchResult(true, null),
            tank => tank.Id == paid.Id ? new Earnings(Cash: 77, Seconds: 0, Hits: 0, Kills: 2) : null);

        Assert.Equal(77, top[0].Cash);
        Assert.Null(top[1].Cash);
    }

    [Fact]
    public void EntriesCarryWhatTheScreenNeedsToDrawTheTank()
    {
        var bot = new Tank { Name = "Bot", IsBot = true, Team = 2 };

        var entry = Build([bot], new MatchResult(true, null)).Single();

        Assert.Equal((bot.Id, "Bot", true, 2), (entry.TankId, entry.Name, entry.IsBot, entry.Team));
    }
}

public class StandingsInGameTests
{
    [Fact]
    public async Task StandingsAppearOnlyOnceTheMatchHasEnded()
    {
        var store = new InMemoryProfileStore();
        var winnerProfile = await store.CreateAsync();
        var game = TestGames.NewGame(new MatchSettings { Health = 1, Lives = 1 }, profileStore: store);
        var winner = game.JoinGame("A", winnerProfile.Id);
        var loser = game.JoinGame("B");
        Assert.Null(game.GetGameState().Standings);

        TestGames.ShootAt(game, winner, loser);
        await TestGames.TickUntil(game, () => game.Status == GameStatus.Ended);

        var standings = game.GetGameState().Standings!;
        Assert.Equal(winner, standings[0].TankId);
        Assert.True(standings[0].Won);
        Assert.Equal(1, standings[0].Kills);
        Assert.True(standings[0].Cash >= CurrencyRates.Default.CashPerHit + CurrencyRates.Default.CashPerKill);
        Assert.Equal(loser, standings[1].TankId);
        Assert.Null(standings[1].Cash);
    }
}
