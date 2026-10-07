using GameLogic;

namespace GameTest;

public class PendingSpawnTests
{
    private static readonly MatchSettings Match = new();

    private static Tank Waiting(int ticks = 10) =>
        new() { PositionX = 400, PositionY = 200, Health = 0, Deaths = 1, RespawnTicksLeft = ticks };

    [Fact]
    public void DefaultRespawnDelayIsThreeSeconds()
    {
        Assert.Equal(3, new MatchSettings().RespawnSeconds);
    }

    [Fact]
    public void WaitingTankGetsASpawnPickedAheadOfTime()
    {
        var tank = Combat.TickRespawns([Waiting()], TestGames.Arena, Match, new Random(1)).Single();

        Assert.True(tank.Respawning);
        Assert.Contains(tank.PendingSpawn, TestGames.Arena.SpawnPoints);
    }

    [Fact]
    public void PendingSpawnStaysPutWhileItIsStillFree()
    {
        var first = Combat.TickRespawns([Waiting()], TestGames.Arena, Match, new Random(1));
        var second = Combat.TickRespawns(first, TestGames.Arena, Match, new Random(2));

        Assert.Equal(first.Single().PendingSpawn, second.Single().PendingSpawn);
    }

    [Fact]
    public void TankComesBackAtTheSpawnItWasShown()
    {
        var shown = new MapSpawnPoint(700, 60, 180);
        var waiting = Waiting(ticks: 1) with { PendingSpawn = shown };

        var back = Combat.TickRespawns([waiting], TestGames.Arena, Match, new FirstSpawnRandom()).Single();

        Assert.False(back.Respawning);
        Assert.Equal((shown.X, shown.Y), (back.PositionX, back.PositionY));
        Assert.Null(back.PendingSpawn);
    }

    [Fact]
    public void PendingSpawnIsPickedAgainWhenSomeoneParksOnIt()
    {
        var blocked = new MapSpawnPoint(700, 60, 180);
        var waiting = Waiting() with { PendingSpawn = blocked };
        var squatter = new Tank { PositionX = 700, PositionY = 60 };

        var result = Combat.TickRespawns([waiting, squatter], TestGames.Arena, Match, new FirstSpawnRandom());

        var pending = result[0].PendingSpawn;
        Assert.NotNull(pending);
        Assert.NotEqual(blocked, pending);
    }

    [Fact]
    public void OnlyTheOwnerSeesTheirPendingSpawn()
    {
        var game = TestGames.NewGame();
        var me = game.JoinGame();
        var enemy = game.JoinGame();
        var spawn = new MapSpawnPoint(700, 60, 180);
        game.Tanks = game.Tanks.Select(t => t.Id == me ? t with { Health = 0, RespawnTicksLeft = 20, PendingSpawn = spawn } : t).ToArray();

        var mine = game.GetGameState(viewerId: me).Tanks!.Single(t => t.Id == me);
        var seenByEnemy = game.GetGameState(viewerId: enemy).Tanks!.Single(t => t.Id == me);
        var seenByNobody = game.GetGameState().Tanks!.Single(t => t.Id == me);

        Assert.Equal(spawn, mine.PendingSpawn);
        Assert.Null(seenByEnemy.PendingSpawn);
        Assert.Null(seenByNobody.PendingSpawn);
    }
}
