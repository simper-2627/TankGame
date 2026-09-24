using GameLogic;
using GameLogic.Game;
using Microsoft.AspNetCore.SignalR;

namespace GameTest;

public class UnitTest1
{
    private sealed class PausingObstacles : IReadOnlyList<Obstacle>
    {
        public ManualResetEventSlim Entered { get; } = new(false);
        public ManualResetEventSlim Resume { get; } = new(false);
        public int Count => 1;
        public Obstacle this[int index] => new(350, 350, 10, 10);
        public IEnumerator<Obstacle> GetEnumerator()
        {
            Entered.Set();
            if (!Resume.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("Test did not release the game tick.");
            yield return this[0];
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public async Task ReverseInputDuringTickIsNotOverwritten()
    {
        var obstacles = new PausingObstacles();
        var game = new Game(new TestHubContext())
        {
            Map = new GameMap("Concurrent", 400, 400, obstacles, [new MapSpawnPoint(60, 60, 0)])
        };
        var id = game.JoinGame();
        var input = new PlayerInputRequest { GameName = "Concurrent", PlayerId = id,
            Forward = true, Backward = false, Left = false, Right = false, Shoot = false,
            LastDirectionBackwards = false };
        game.ReceiveUserInput(input);
        var tick = Task.Run(() => game.loopRunner.ProcessGameTick());
        Task reverse = Task.CompletedTask;
        try
        {
            Assert.True(obstacles.Entered.Wait(TimeSpan.FromSeconds(5)));
            reverse = Task.Run(() => game.ReceiveUserInput(input with
            {
                Forward = false, Backward = true, LastDirectionBackwards = true
            }));
            await Task.WhenAny(reverse, Task.Delay(100));
        }
        finally
        {
            obstacles.Resume.Set();
            await Task.WhenAll(tick, reverse);
        }
        Assert.True(game.Tanks.Single().MovingBackward);
        Assert.True(game.Tanks.Single().LastDirectionWasBackwards);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReverseEscapesReportedCenterWallContact(bool sliding)
    {
        var map = MapCatalog.GetByName("Center Wall");
        var tank = new Tank { PositionX = 338, PositionY = 269, Angle = 50,
            MovingBackward = true, LastDirectionWasBackwards = true };
        var settings = new DeveloperGameSettings { SlideAlongWalls = sliding };

        var moved = Tank.ProcessTankMovement(tank, map, settings);

        Assert.True(moved.PositionX < tank.PositionX);
        Assert.True(moved.PositionY < tank.PositionY);
        Assert.False(map.Blocks(Tank.GetCollisionArea(moved, settings)));
    }

    [Theory]
    [InlineData(68, 94, 45)]
    [InlineData(192, 94, 135)]
    [InlineData(68, 218, -45)]
    [InlineData(192, 218, 225)]
    public void ExactCornerContactDoesNotChooseAnArbitraryTrajectory(int x, int y, int angle)
    {
        var map = new GameMap("Corner", 400, 400, [new Obstacle(120, 120, 80, 80)], []);
        var tank = new Tank { PositionX = x, PositionY = y, Angle = angle, MovingForward = true, Speed = 80 };
        var settings = new DeveloperGameSettings { SlideAlongWalls = true };

        var moved = Tank.ProcessTankMovement(tank, map, settings);

        Assert.Equal(tank.PositionX, moved.PositionX);
        Assert.Equal(tank.PositionY, moved.PositionY);
        Assert.Equal(0, moved.Speed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiagonalWallContactSlidesOnlyWhenEnabled(bool sliding)
    {
        var map = new GameMap("Wall", 400, 400, [new Obstacle(120, 0, 1, 300)], []);
        var tank = new Tank { PositionX = 68, PositionY = 100, Angle = 45, MovingForward = true, Speed = 80 };
        var settings = new DeveloperGameSettings { SlideAlongWalls = sliding, CollisionStepPixels = 12 };

        var moved = Tank.ProcessTankMovement(tank, map, settings);

        Assert.Equal(68, moved.PositionX);
        Assert.Equal(sliding ? 121 : 100, moved.PositionY);
        Assert.False(map.Blocks(Tank.GetCollisionArea(moved, settings)));
    }

    [Fact]
    public void SlidingStopsAtInsideCornerAndCanReverseOut()
    {
        var map = new GameMap("Corner", 400, 400,
            [new Obstacle(120, 0, 20, 300), new Obstacle(0, 150, 300, 20)], []);
        var settings = new DeveloperGameSettings { SlideAlongWalls = true };
        var tank = new Tank { PositionX = 68, PositionY = 124, Angle = 45, MovingForward = true, Speed = 80 };

        var stopped = Tank.ProcessTankMovement(tank, map, settings);
        Assert.Equal(tank.PositionX, stopped.PositionX);
        Assert.Equal(tank.PositionY, stopped.PositionY);
        Assert.Equal(0, stopped.Speed);

        var reversed = Tank.ProcessTankMovement(stopped with
        {
            MovingForward = false, MovingBackward = true, LastDirectionWasBackwards = true
        }, map, settings);
        Assert.True(reversed.PositionX < stopped.PositionX);
        Assert.True(reversed.PositionY < stopped.PositionY);
        Assert.False(map.Blocks(Tank.GetCollisionArea(reversed, settings)));
    }

    [Fact]
    public void SlidingRoundsOutsideCornerWithoutCrossingWall()
    {
        var map = new GameMap("Corner", 400, 400, [new Obstacle(120, 0, 20, 120)], []);
        var settings = new DeveloperGameSettings { SlideAlongWalls = true };
        var tank = new Tank { PositionX = 68, PositionY = 130, Angle = 45, MovingForward = true, Speed = 80 };

        var moved = Tank.ProcessTankMovement(tank, map, settings);

        Assert.True(moved.PositionX > tank.PositionX);
        Assert.True(moved.PositionY > tank.PositionY);
        Assert.False(map.Blocks(Tank.GetCollisionArea(moved, settings)));
    }

    [Fact]
    public void DeveloperSettingsCarrySlidingAndResetToDefault()
    {
        var game = new Game(new TestHubContext()) { MatchType = GameMatchTypes.DeveloperSimulation };
        game.UpdateDeveloperSettings(new DeveloperGameSettings { SlideAlongWalls = true });
        Assert.True(game.GetGameState().DeveloperSettings.SlideAlongWalls);
        game.UpdateDeveloperSettings(new DeveloperGameSettings());
        Assert.False(game.GetGameState().DeveloperSettings.SlideAlongWalls);
    }

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
        Assert.True(
            bullet.PositionX != updatedBullet.PositionX ||
            bullet.PositionY != updatedBullet.PositionY);
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
        var map = new GameMap("Test", 120, 120, [], [new MapSpawnPoint(0, 0, 0)]);
        var tank = new Tank { PositionX = 50, PositionY = 50, Angle = 0, MovingForward = true, Speed = 80 };

        var movedTank = Tank.ProcessTankMovement(tank, map);

        Assert.InRange(movedTank.PositionX, 0, 60);
        Assert.InRange(movedTank.PositionY, 0, 60);
    }

    [Fact]
    public void TankMovementIsBlockedByObstacle()
    {
        var map = new GameMap("Test", 300, 300, [new Obstacle(120, 30, 80, 80)], [new MapSpawnPoint(0, 0, 0)]);
        var tank = new Tank { PositionX = 50, PositionY = 50, Angle = 0, MovingForward = true, Speed = 30 };

        var movedTank = Tank.ProcessTankMovement(tank, map);

        Assert.Equal(68, movedTank.PositionX);
        Assert.Equal(tank.PositionY, movedTank.PositionY);
        Assert.Equal(0, movedTank.Speed);
    }

    [Fact]
    public void BulletStopsWhenItHitsObstacle()
    {
        var map = new GameMap("Test", 300, 300, [new Obstacle(65, 50, 30, 30)], [new MapSpawnPoint(0, 0, 0)]);
        var bullet = new Bullet { PositionX = 50, PositionY = 60, Angle = 0 };

        var movedBullet = Bullet.MoveBullet(bullet, map);

        Assert.Null(movedBullet);
    }

    [Fact]
    public void BulletStopsWhenItLeavesMap()
    {
        var map = new GameMap("Test", 60, 60, [], [new MapSpawnPoint(0, 0, 0)]);
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
    public void MapCatalogHasValidSpawnPointsOutsideObstacles()
    {
        Assert.All(MapCatalog.FixedMaps, map =>
        {
            Assert.NotEmpty(map.SpawnPoints);
            Assert.All(map.SpawnPoints, spawnPoint =>
            {
                var spawnTank = new Tank
                {
                    PositionX = spawnPoint.X,
                    PositionY = spawnPoint.Y,
                    Angle = spawnPoint.Angle
                };

                Assert.False(map.Blocks(Tank.GetCollisionArea(spawnTank)));
            });
        });
    }

    [Fact]
    public void MapCatalogFindsFixedMapByName()
    {
        var secondMap = MapCatalog.FixedMaps[1];

        var selectedMap = MapCatalog.GetByName(secondMap.Name);

        Assert.Equal(secondMap.Name, selectedMap.Name);
    }

    [Fact]
    public void LobbyCreateGameUsesSelectedMap()
    {
        var hubContext = new TestHubContext();
        var lobby = new Lobby(hubContext);
        var selectedMap = MapCatalog.FixedMaps[2];

        var game = lobby.CreateGame("selected-map-game", selectedMap.Name);

        Assert.Equal(selectedMap.Name, game.Map.Name);
        Assert.Equal(game.Map.Name, game.GetGameState().Map?.Name);
    }

    [Fact]
    public void JoinGameUsesMapSpecificSpawnPoints()
    {
        var hubContext = new TestHubContext();
        var game = new Game(hubContext)
        {
            Map = MapCatalog.FixedMaps[1]
        };
        var expectedSpawnPoint = game.Map.SpawnPoints[0];

        var playerId = game.JoinGame();
        var tank = game.Tanks.Single(tank => tank.Id == playerId);

        Assert.Equal(expectedSpawnPoint.X, tank.PositionX);
        Assert.Equal(expectedSpawnPoint.Y, tank.PositionY);
        Assert.Equal(expectedSpawnPoint.Angle, tank.Angle);
    }
}
