using GameLogic;
using GameLogic.Game;
using Microsoft.AspNetCore.SignalR;

namespace GameTest;

// Hub context that drops every message, for tests that only check game state
internal sealed class FakeHubContext : IHubContext<LobbyHub>
{
    public IHubClients Clients { get; } = new NoOpClients();
    public IGroupManager Groups { get; } = new NoOpGroups();

    private sealed class NoOpProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NoOpClients : IHubClients
    {
        private static readonly IClientProxy proxy = new NoOpProxy();
        public IClientProxy All => proxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => proxy;
        public IClientProxy Client(string connectionId) => proxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => proxy;
        public IClientProxy Group(string groupName) => proxy;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => proxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => proxy;
        public IClientProxy User(string userId) => proxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => proxy;
    }

    private sealed class NoOpGroups : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

// Reload is measured in real milliseconds; tests move time by hand instead of sleeping
internal sealed class FakeClock
{
    private long now = 1_000_000;
    public long Now() => now;
    public void Advance(int ms) => now += ms;
}

internal sealed class FirstSpawnRandom : Random
{
    public override int Next(int maxValue) => 0;
}

internal static class TestGames
{
    // Open map: spawns 1 and 2 face each other on the same row, spawn 3 is far away in a corner
    public static readonly GameMap Arena = new("Arena", 800, 400, [],
        [new MapSpawnPoint(100, 200, 0), new MapSpawnPoint(400, 200, 180), new MapSpawnPoint(700, 60, 180)]);

    public static Game NewGame(MatchSettings? settings = null, FakeClock? clock = null,
        string matchType = GameMatchTypes.Multiplayer, Random? botRandom = null) =>
        new(new FakeHubContext())
        {
            Map = Arena,
            MatchType = matchType,
            SpawnRandom = new FirstSpawnRandom(),
            BotRandom = botRandom ?? new Random(1),
            Settings = settings ?? new MatchSettings(),
            Clock = (clock ?? new FakeClock()).Now,
        };

    public static PlayerInputRequest Input(Guid playerId, bool shoot = false, int? aimX = null, int? aimY = null) => new()
    {
        GameName = "test", PlayerId = playerId,
        Up = false, Down = false, Left = false, Right = false,
        Shoot = shoot, Boost = false, AimX = aimX, AimY = aimY,
    };

    // Aim at the target's center, then press fire (release first so it counts as a new press)
    public static void ShootAt(Game game, Guid shooter, Guid target)
    {
        var (x, y) = Tank.GetCenter(game.Tanks.Single(t => t.Id == target), game.DeveloperSettings);
        game.ReceiveUserInput(Input(shooter, shoot: false, aimX: x, aimY: y));
        game.ReceiveUserInput(Input(shooter, shoot: true, aimX: x, aimY: y));
    }

    public static async Task TickUntil(Game game, Func<bool> done, int maxTicks = 100)
    {
        for (var tick = 0; tick < maxTicks && !done(); tick++)
            await game.loopRunner.ProcessGameTick();
        Assert.True(done(), $"condition not reached within {maxTicks} ticks");
    }
}
