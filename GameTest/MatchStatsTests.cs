using GameLogic;
using GameLogic.Game;

namespace GameTest;

public class MatchStatsTests
{
    private static Tank TankAt(int x, int health = 3) => new() { PositionX = x, PositionY = 200, Health = health };

    private static TankState Of(Game game, Guid viewer, Guid tank) =>
        game.GetGameState(viewerId: viewer).Tanks!.Single(t => t.Id == tank);

    private static void Press(Game game, Guid id)
    {
        game.ReceiveUserInput(TestGames.Input(id, shoot: false));
        game.ReceiveUserInput(TestGames.Input(id, shoot: true));
    }

    [Fact]
    public void KillingBlowCountsAsAKill()
    {
        var tanks = new List<Tank> { TankAt(100), TankAt(400, health: 1) };

        Combat.ApplyHit(tanks, 1, tanks[0].Id, new MatchSettings { Lives = 1 });

        Assert.Equal(1, tanks[0].Kills);
        Assert.Equal(1, tanks[0].HitsLanded);
        Assert.Equal(1, tanks[1].HitsTaken);
    }

    [Fact]
    public void HitThatLeavesHealthIsNotAKill()
    {
        var tanks = new List<Tank> { TankAt(100), TankAt(400, health: 3) };

        Combat.ApplyHit(tanks, 1, tanks[0].Id, new MatchSettings());
        Combat.ApplyHit(tanks, 1, tanks[0].Id, new MatchSettings());

        Assert.Equal(0, tanks[0].Kills);
        Assert.Equal(2, tanks[0].HitsLanded);
        Assert.Equal(2, tanks[1].HitsTaken);
    }

    [Fact]
    public void BouncingIntoYourselfIsNotAKill()
    {
        var tanks = new List<Tank> { TankAt(100, health: 1) };

        Combat.ApplyHit(tanks, 0, tanks[0].Id, new MatchSettings { Lives = 1 });

        Assert.Equal(0, tanks[0].Kills);
        Assert.Equal(0, tanks[0].HitsLanded);
        Assert.Equal(1, tanks[0].HitsTaken);
    }

    [Fact]
    public void ShotsFiredCountsPressesThatFireOnly()
    {
        var clock = new FakeClock();
        var game = TestGames.NewGame(new MatchSettings { ReloadMs = 300 }, clock);
        var a = game.JoinGame();
        game.JoinGame();

        Press(game, a);
        game.ReceiveUserInput(TestGames.Input(a, shoot: true)); // still held
        Press(game, a); // during reload
        clock.Advance(300);
        Press(game, a);

        Assert.Equal(2, game.Tanks.Single(t => t.Id == a).ShotsFired);
    }

    [Fact]
    public void PracticeShotsBeforeTheSecondPlayerJoinsDoNotCount()
    {
        var clock = new FakeClock();
        var game = TestGames.NewGame(new MatchSettings { ReloadMs = 300 }, clock);
        var a = game.JoinGame();

        Press(game, a);
        clock.Advance(300);
        game.JoinGame();
        Press(game, a);

        Assert.Equal(1, game.Tanks.Single(t => t.Id == a).ShotsFired);
    }

    [Fact]
    public async Task EndedDuelRecordsTimesAndPlacements()
    {
        var game = TestGames.NewGame(new MatchSettings { Health = 1, Lives = 1 });
        var a = game.JoinGame();
        var b = game.JoinGame();
        Assert.Equal(0, game.MatchSeconds);
        for (var tick = 0; tick < 25; tick++)
            await game.loopRunner.ProcessGameTick();
        TestGames.ShootAt(game, a, b);
        await TestGames.TickUntil(game, () => game.Status == GameStatus.Ended);

        var state = game.GetGameState();
        var winner = state.Tanks!.Single(t => t.Id == a);
        var loser = state.Tanks!.Single(t => t.Id == b);
        Assert.Equal(game.Tick, game.EndedAtTick);
        Assert.Equal(game.Tick / GameLoopRunner.TicksPerSecond, state.MatchSeconds);
        Assert.Equal(game.Tick, game.Tanks.Single(t => t.Id == b).EliminatedAtTick);
        Assert.Equal(1, winner.Placement);
        Assert.Equal(2, loser.Placement);
        Assert.Equal(1, winner.Kills);
        Assert.Equal(1, winner.ShotsFired);
        Assert.Equal(1, loser.HitsTaken);
        Assert.Equal(state.MatchSeconds, winner.SecondsSurvived);
    }

    [Fact]
    public void InstantShotStampsTheElimination()
    {
        var game = TestGames.NewGame(new MatchSettings { Health = 1, Lives = 1, Projectile = ProjectileType.Realistic });
        var a = game.JoinGame();
        var b = game.JoinGame();

        TestGames.ShootAt(game, a, b);

        Assert.Equal(game.Tick, game.Tanks.Single(t => t.Id == b).EliminatedAtTick);
        Assert.Equal(1, game.Tanks.Single(t => t.Id == a).Kills);
    }

    [Fact]
    public void MatchClockWaitsForTheSecondPlayer()
    {
        var game = TestGames.NewGame();
        game.JoinGame();

        Assert.Null(game.GetGameState().MatchSeconds);
    }

    [Fact]
    public void StatsArePrivateUntilTheMatchEnds()
    {
        var game = TestGames.NewGame();
        var me = game.JoinGame();
        var enemy = game.JoinGame();

        var mine = Of(game, me, me);
        var asSeenByEnemy = Of(game, enemy, me);

        Assert.Equal(0, mine.Kills);
        Assert.Equal(0, mine.ShotsFired);
        Assert.Equal(0, mine.HitsTaken);
        Assert.NotNull(mine.SecondsSurvived);
        Assert.Null(mine.Placement);
        Assert.Null(asSeenByEnemy.Kills);
        Assert.Null(asSeenByEnemy.ShotsFired);
        Assert.Null(asSeenByEnemy.HitsTaken);
        Assert.Null(asSeenByEnemy.SecondsSurvived);
    }

    [Fact]
    public async Task StatsAreRevealedOnceTheMatchEnds()
    {
        var game = TestGames.NewGame(new MatchSettings { Health = 1, Lives = 1 });
        var me = game.JoinGame();
        var enemy = game.JoinGame();
        TestGames.ShootAt(game, me, enemy);
        await TestGames.TickUntil(game, () => game.Status == GameStatus.Ended);

        var asSeenByEnemy = Of(game, enemy, me);

        Assert.Equal(1, asSeenByEnemy.Kills);
        Assert.Equal(1, asSeenByEnemy.ShotsFired);
        Assert.NotNull(asSeenByEnemy.HitsTaken);
        Assert.NotNull(asSeenByEnemy.SecondsSurvived);
        Assert.Equal(1, asSeenByEnemy.Placement);
    }

    [Fact]
    public void RankingPutsTheWinnerThenSurvivorsThenTheLastOut()
    {
        var firstOut = new Tank { Eliminated = true, EliminatedAtTick = 10, Kills = 5 };
        var lastOut = new Tank { Eliminated = true, EliminatedAtTick = 50 };
        var survivor = new Tank { Deaths = 2 };
        var winner = new Tank { Deaths = 3 };

        var ranked = MatchSummary.Rank([firstOut, lastOut, survivor, winner], winner.Id);

        Assert.Equal([winner.Id, survivor.Id, lastOut.Id, firstOut.Id], ranked.Select(t => t.Id));
    }

    [Fact]
    public void RankingBreaksTiesOnKillsThenHits()
    {
        var fewerKills = new Tank { Eliminated = true, EliminatedAtTick = 20, Kills = 1, HitsLanded = 9 };
        var moreKills = new Tank { Eliminated = true, EliminatedAtTick = 20, Kills = 2 };
        var moreHits = new Tank { Eliminated = true, EliminatedAtTick = 20, Kills = 1, HitsLanded = 10 };

        var ranked = MatchSummary.Rank([fewerKills, moreKills, moreHits], null);

        Assert.Equal([moreKills.Id, moreHits.Id, fewerKills.Id], ranked.Select(t => t.Id));
    }
}
