using GameLogic;
using GameLogic.Game;
using Microsoft.AspNetCore.SignalR;

namespace GameTest;

public class HiddenValuesTests
{
    private static (Game Game, Guid Me, Guid Enemy, FakeClock Clock) Duel(MatchSettings? settings = null)
    {
        var clock = new FakeClock();
        var game = TestGames.NewGame(settings ?? new MatchSettings { Health = 5, Lives = 4, ReloadMs = 1000 }, clock);
        return (game, game.JoinGame(), game.JoinGame(), clock);
    }

    private static TankState Of(Game game, Guid viewer, Guid tank) =>
        game.GetGameState(viewerId: viewer).Tanks!.Single(t => t.Id == tank);

    [Fact]
    public void OwnerSeesTheirHealthAndLivesButEnemiesDoNot()
    {
        var (game, me, enemy, _) = Duel();

        var mine = Of(game, me, me);
        var asSeenByEnemy = Of(game, enemy, me);

        Assert.Equal(5, mine.Health);
        Assert.Equal(0, mine.Deaths);
        Assert.Null(asSeenByEnemy.Health);
        Assert.Null(asSeenByEnemy.Deaths);
        Assert.Null(game.GetGameState().Tanks!.First().Health);
    }

    [Fact]
    public void DestroyedTankStillReadsAsRespawningToEveryone()
    {
        var (game, me, enemy, _) = Duel();
        game.Tanks = game.Tanks.Select(t => t.Id == me ? t with { Health = 0, Deaths = 1, RespawnTicksLeft = 20 } : t).ToArray();

        var asSeenByEnemy = Of(game, enemy, me);

        Assert.True(asSeenByEnemy.Respawning);
        Assert.Null(asSeenByEnemy.Deaths);
    }

    [Fact]
    public void EliminatedTankStaysEliminatedToEveryone()
    {
        var (game, me, enemy, _) = Duel();
        game.Tanks = game.Tanks.Select(t => t.Id == me ? t with { Health = 0, Deaths = 4, Eliminated = true } : t).ToArray();

        var asSeenByEnemy = Of(game, enemy, me);

        Assert.True(asSeenByEnemy.Eliminated);
        Assert.False(asSeenByEnemy.Respawning);
    }

    [Fact]
    public async Task EverythingIsRevealedOnceTheMatchEnds()
    {
        var (game, me, enemy, _) = Duel(new MatchSettings { Health = 1, Lives = 1 });
        TestGames.ShootAt(game, me, enemy);
        await TestGames.TickUntil(game, () => game.Status == GameStatus.Ended);

        var asSeenByEnemy = Of(game, enemy, me);

        Assert.NotNull(asSeenByEnemy.Health);
        Assert.NotNull(asSeenByEnemy.Deaths);
    }

    [Fact]
    public void ReloadIsOnlyReportedToTheOwner()
    {
        var (game, me, enemy, clock) = Duel();
        Assert.Equal(0, Of(game, me, me).ReloadMsLeft);

        TestGames.ShootAt(game, me, enemy);
        clock.Advance(400);

        Assert.Equal(600, Of(game, me, me).ReloadMsLeft);
        Assert.Null(Of(game, enemy, me).ReloadMsLeft);
        Assert.Null(game.GetGameState().Tanks!.First().ReloadMsLeft);
    }

    [Fact]
    public void ReloadReadsZeroOnceFinished()
    {
        var (game, me, enemy, clock) = Duel();
        TestGames.ShootAt(game, me, enemy);
        clock.Advance(5000);

        Assert.Equal(0, Of(game, me, me).ReloadMsLeft);
    }

    [Fact]
    public async Task BroadcastGivesEachPlayerOnlyTheirOwnSecrets()
    {
        var sent = new Dictionary<string, GameState>();
        var game = new Game(new RecordingHubContext(sent))
        {
            Map = TestGames.Arena, SpawnRandom = new FirstSpawnRandom(), Settings = new MatchSettings { Health = 5 },
        };
        var a = game.JoinGame();
        var b = game.JoinGame();
        game.ConnectedClients["conn-a"] = a;
        game.ConnectedClients["conn-b"] = b;
        game.ConnectedClients["conn-watcher"] = null;

        await game.BroadcastUpdate();

        Assert.Equal(5, sent["conn-a"].Tanks!.Single(t => t.Id == a).Health);
        Assert.Null(sent["conn-a"].Tanks!.Single(t => t.Id == b).Health);
        Assert.Equal(5, sent["conn-b"].Tanks!.Single(t => t.Id == b).Health);
        Assert.Null(sent["conn-b"].Tanks!.Single(t => t.Id == a).Health);
        Assert.All(sent["conn-watcher"].Tanks!, t => Assert.Null(t.Health));
    }

    // Remembers the last GameState sent to each connection id
    private sealed class RecordingHubContext(Dictionary<string, GameState> sent) : IHubContext<LobbyHub>
    {
        public IHubClients Clients { get; } = new RecordingClients(sent);
        public IGroupManager Groups => throw new NotSupportedException();

        private sealed class RecordingClients(Dictionary<string, GameState> sent) : IHubClients
        {
            private IClientProxy To(IEnumerable<string> ids) => new Proxy(sent, ids.ToArray());
            public IClientProxy Client(string connectionId) => To([connectionId]);
            public IClientProxy Clients(IReadOnlyList<string> connectionIds) => To(connectionIds);
            public IClientProxy All => throw new NotSupportedException();
            public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
            public IClientProxy Group(string groupName) => throw new NotSupportedException();
            public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
            public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
            public IClientProxy User(string userId) => throw new NotSupportedException();
            public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
        }

        private sealed class Proxy(Dictionary<string, GameState> sent, string[] ids) : IClientProxy
        {
            public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
            {
                foreach (var id in ids)
                    sent[id] = (GameState)args[0]!;
                return Task.CompletedTask;
            }
        }
    }
}
