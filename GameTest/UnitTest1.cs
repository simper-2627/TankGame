using GameLogic;
using GameLogic.Game;
using Microsoft.AspNetCore.SignalR;

namespace GameTest;

public class UnitTest1
{
    private class TestHubClients : IHubClients
    {
        public IClientProxy this[string connectionId] => new TestClientProxy();

        public IClientProxy All => new TestClientProxy();

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => new TestClientProxy();

        public IClientProxy Client(string connectionId) => new TestClientProxy();

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new TestClientProxy();

        public IClientProxy Group(string groupName) => new TestClientProxy();

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => new TestClientProxy();

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => new TestClientProxy();

        public IClientProxy User(string userId) => new TestClientProxy();

        public IClientProxy Users(IReadOnlyList<string> userIds) => new TestClientProxy();
    }

    private class TestClientProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private class TestGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
    private class TestHubContext : IHubContext<LobbyHub>
    {
        public IHubClients Clients { get; } = new TestHubClients();
        public IGroupManager Groups { get; } = new TestGroupManager();
    }

    [Fact]
    public async Task BulletMotion()
    {
        
        var hubContext = new TestHubContext();
        Game game = new(hubContext);
        var playerId = game.JoinGame();

        var playerInput = new PlayerInputRequest
        {
            GameName = "TestGame",
            PlayerId = playerId,
            Forward = false,
            Left = false,
            Right = false,
            Backward = false,
            Shoot = true,
            LastDirectionBackwards = false
        };

        game.ReceiveUserInput(playerInput);

        var gameState = game.GetGameState();
        var bullet = gameState.Bullets!.FirstOrDefault();
        Assert.NotNull(bullet);
        Assert.Equal(game.Tanks.First().PositionX, bullet.PositionX);
        Assert.Equal(game.Tanks.First().PositionY, bullet.PositionY);

        await game.loopRunner.ProcessGameTick();

        var updatedGameState = game.GetGameState();
        var updatedBullet = updatedGameState.Bullets!.FirstOrDefault();
        Assert.NotNull(updatedBullet);
        Assert.NotEqual(bullet.PositionX, updatedBullet.PositionX);
        Assert.NotEqual(bullet.PositionY, updatedBullet.PositionY);
    }

    [Fact]
    public void ObstacleDetectsPointInsideAndOutside()
    {
        var obstacle = new Obstacle(10, 20, 30, 40);

        Assert.True(obstacle.ContainsPoint(15, 25));
        Assert.False(obstacle.ContainsPoint(5, 25));
    }

    [Fact]
    public void ObstacleDetectsRectangleIntersections()
    {
        var obstacle = new Obstacle(100, 100, 50, 50);

        Assert.True(obstacle.Intersects(new RectangleArea(125, 125, 25, 25)));
        Assert.False(obstacle.Intersects(new RectangleArea(200, 200, 25, 25)));
    }

    [Fact]
    public void TankMovementStaysInsideMapBounds()
    {
        var map = new GameMap("Test", 120, 120, []);
        var tank = new Tank { PositionX = 100, PositionY = 100, MovingForward = true, Speed = 80 };

        var movedTank = Tank.ProcessTankMovement(tank, map);

        Assert.InRange(movedTank.PositionX, 0, 60);
        Assert.InRange(movedTank.PositionY, 0, 60);
    }

    [Fact]
    public void TankMovementIsBlockedByObstacle()
    {
        var map = new GameMap("Test", 300, 300, [new Obstacle(80, 30, 80, 80)]);
        var tank = new Tank { PositionX = 50, PositionY = 50, Angle = 0, MovingForward = true, Speed = 30 };

        var movedTank = Tank.ProcessTankMovement(tank, map);

        Assert.Equal(tank.PositionX, movedTank.PositionX);
        Assert.Equal(tank.PositionY, movedTank.PositionY);
    }

    [Fact]
    public void BulletStopsWhenItHitsObstacle()
    {
        var map = new GameMap("Test", 300, 300, [new Obstacle(65, 50, 30, 30)]);
        var bullet = new Bullet { PositionX = 50, PositionY = 60, Angle = 0 };

        var movedBullet = Bullet.MoveBullet(bullet, map);

        Assert.Null(movedBullet);
    }

    [Fact]
    public void BulletStopsWhenItLeavesMap()
    {
        var map = new GameMap("Test", 60, 60, []);
        var bullet = new Bullet { PositionX = 55, PositionY = 20, Angle = 0 };

        var movedBullet = Bullet.MoveBullet(bullet, map);

        Assert.Null(movedBullet);
    }

    [Fact]
    public void GameStateIncludesCurrentMap()
    {
        var hubContext = new TestHubContext();
        var game = new Game(hubContext);

        var gameState = game.GetGameState();

        Assert.NotNull(gameState.Map);
        Assert.Equal(game.Map.Name, gameState.Map.Name);
    }

    [Fact]
    public void MapCatalogHasFixedMapsWithObstacles()
    {
        Assert.Equal(4, MapCatalog.FixedMaps.Count);
        Assert.All(MapCatalog.FixedMaps, map => Assert.NotEmpty(map.Obstacles));
    }

    [Fact]
    public void MapCatalogRotatesThroughFixedMaps()
    {
        var firstMap = MapCatalog.FixedMaps[0];
        var secondMap = MapCatalog.FixedMaps[1];

        var nextMap = MapCatalog.NextMap(firstMap);

        Assert.Equal(secondMap.Name, nextMap.Name);
    }

    [Fact]
    public async Task GameRotateMapChangesSharedMap()
    {
        var hubContext = new TestHubContext();
        var game = new Game(hubContext);
        var originalMap = game.Map;

        await game.RotateMap();

        Assert.NotEqual(originalMap.Name, game.Map.Name);
        Assert.Equal(game.Map.Name, game.GetGameState().Map?.Name);
    }
}
