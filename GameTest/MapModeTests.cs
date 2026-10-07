using GameLogic;
using GameLogic.Game;
using System.Text.Json;

namespace GameTest;

public class MapModeTests
{
    [Theory]
    [InlineData(MapMode.BigMap, 12, 20, 27, 1800, 1400)]
    [InlineData(MapMode.Foggish, 3, 40, 80, 4500, 2800)]
    public void CatalogMatchesModeAndAllSpawnsAreClear(MapMode mode, int count, int cap, int spawns, int width, int height)
    {
        var maps = MapCatalog.FixedMaps.Where(m => m.Mode == mode).ToArray();
        Assert.Equal(count, maps.Length);
        foreach (var map in maps)
        {
            Assert.Equal((width, height, cap, spawns), (map.Width, map.Height, map.MaxPlayers, map.SpawnPoints.Count));
            Assert.All(map.SpawnPoints, p => Assert.False(map.Blocks(new RectangleArea(p.X, p.Y - 26, 60, 60))));
            Assert.All(map.Obstacles, o => Assert.True(map.Contains(new RectangleArea(o.X, o.Y, o.Width, o.Height))));
        }
    }

    [Theory]
    [InlineData(MapMode.BigMap)]
    [InlineData(MapMode.Foggish)]
    public void LobbyEnforcesCapacityEvenWhenPlayersAreWaiting(MapMode mode)
    {
        var game = new Game(new FakeHubContext()) { Map = MapCatalog.FixedMaps.First(m => m.Mode == mode) };
        Parallel.For(0, game.Map.MaxPlayers, _ => game.JoinGame());
        Assert.Equal(game.Map.MaxPlayers, game.Tanks.Count());
        Assert.Throws<InvalidOperationException>(() => game.JoinGame());
    }

    [Fact]
    public void JoinWaitsAndRespawnsOnceSpaceIsAvailable()
    {
        var map = new GameMap("One spawn", 400, 400, [], [new(100, 100, 0)]);
        var game = new Game(new FakeHubContext()) { Map = map };
        var first = game.JoinGame();
        var second = game.JoinGame();
        Assert.True(game.Tanks.Single(t => t.Id == second).Respawning);
        var result = Combat.TickRespawns(game.Tanks, map, game.Settings, new Random(1));
        Assert.True(result.Single(t => t.Id == second).Respawning);
        result = Combat.TickRespawns(result.Select(t => t.Id == first ? t with { PositionX = 280 } : t), map, game.Settings, new Random(1));
        Assert.False(result.Single(t => t.Id == second).Respawning);
        Assert.Equal(0, result.Single(t => t.Id == second).Deaths);
    }

    [Fact]
    public void SimultaneousRespawnsReserveSpaceImmediately()
    {
        var map = new GameMap("Near spawns", 500, 500, [], [new(100, 100, 0), new(170, 100, 0)]);
        Tank[] tanks = [new() { Health = 0 }, new() { Health = 0 }];
        var result = Combat.TickRespawns(tanks, map, new(), new Random(1));
        Assert.Single(result, t => !t.Respawning);
        Assert.Single(result, t => t.Respawning);
    }

    [Fact]
    public void SpawnClearanceRejectsNearbyLivingTanksButIgnoresEliminatedOnes()
    {
        var map = new GameMap("Spawn", 500, 500, [], [new(100, 100, 0)]);
        var nearby = new Tank { PositionX = 210, PositionY = 100 };
        Assert.Null(SpawnSelector.Choose(map, [nearby], new Random(1)));
        Assert.NotNull(SpawnSelector.Choose(map, [nearby with { Eliminated = true }], new Random(1)));
        Assert.NotNull(SpawnSelector.Choose(map, [nearby with { PositionX = 220 }], new Random(1)));
    }

    [Fact]
    public void RandomSpawningCanReachEveryAvailableSpot()
    {
        var rng = new Random(42);
        var seen = Enumerable.Range(0, 100).Select(_ => SpawnSelector.Choose(TestGames.Arena, [], rng)).Distinct();
        Assert.Equal(TestGames.Arena.SpawnPoints.Count, seen.Count());
    }

    [Theory]
    [InlineData(ShapeKind.Rect, true)]
    [InlineData(ShapeKind.Ellipse, false)]
    [InlineData(ShapeKind.Triangle, false)]
    public void ShapesHaveSolidCentersAndCorrectEmptyCorners(ShapeKind shape, bool cornerSolid)
    {
        var obstacle = new Obstacle(100, 100, 200, 200) { Shape = shape };
        Assert.True(obstacle.ContainsPoint(200, 200));
        Assert.True(obstacle.Intersects(new(190, 190, 20, 20)));
        Assert.Equal(cornerSolid, obstacle.ContainsPoint(101, 101));
        Assert.Equal(cornerSolid, obstacle.Intersects(new(101, 101, 5, 5)));
        Assert.False(obstacle.Intersects(new(310, 150, 30, 30)));
        Assert.True(obstacle.Intersects(new(50, 50, 300, 300)));
        Assert.True(obstacle.Intersects(new(50, 195, 300, 10)));
    }

    [Fact]
    public void ArcHasAnOpenCenterAndAnOpenMissingSector()
    {
        var arc = new Obstacle(100, 100, 200, 200) { Shape = ShapeKind.Arc, InnerRatio = .6, SweepAngle = 180 };
        Assert.False(arc.ContainsPoint(200, 200));
        Assert.False(arc.Intersects(new(190, 190, 20, 20)));
        Assert.False(arc.ContainsPoint(200, 120));
        Assert.True(arc.ContainsPoint(200, 280));
        Assert.True(arc.Intersects(new(190, 275, 20, 10)));
    }

    [Fact]
    public void ShapeCopiesAndSerializationKeepGeometry()
    {
        var original = new Obstacle(100, 100, 200, 200) { Shape = ShapeKind.Ellipse };
        Assert.True(original.ContainsPoint(200, 200));
        var moved = original with { X = 500 };
        Assert.True(moved.ContainsPoint(600, 200));
        Assert.False(moved.ContainsPoint(200, 200));
        var json = JsonSerializer.Serialize(moved);
        Assert.DoesNotContain("Outline", json);
        Assert.True(JsonSerializer.Deserialize<Obstacle>(json)!.ContainsPoint(600, 200));
    }

    [Fact]
    public void BigMapCameraFitsChangedRatiosAndAimUsesWorldCoordinates()
    {
        var map = MapCatalog.FixedMaps.First(m => m.Mode == MapMode.BigMap);
        var camera = MapCamera.For(map, 0, 0);
        Assert.Equal(.5, camera.Scale);
        Assert.Equal((900, 700), camera.ToWorld(450, 350));
        camera = MapCamera.For(map with { Width = 2400, ViewWidth = 1000 }, 0, 0);
        Assert.Equal(.5, camera.Scale);
        Assert.Equal((1000, 700), camera.ToWorld(500, 350));
    }

    [Fact]
    public void FoggishCameraFollowsAndClampsToAllMapEdges()
    {
        var map = MapCatalog.FixedMaps.First(m => m.Mode == MapMode.Foggish);
        Assert.Equal(new MapCamera(.5, 0, 0), MapCamera.For(map, 10, 10));
        var middle = MapCamera.For(map, 2000, 1400);
        Assert.Equal((2000, 1400), middle.ToWorld(450, 350));
        Assert.Equal(new MapCamera(.5, 2700, 1400), MapCamera.For(map, 4500, 2800));
        Assert.Equal(MapCamera.For(MapCatalog.FixedMaps.First(m => m.Mode == MapMode.BigMap), 0, 0).Scale, middle.Scale);
        var bottomRight = MapCamera.For(map, 4500, 2800);
        Assert.Equal((4500, 2800), bottomRight.ToWorld(map.ViewWidth, map.ViewHeight));
    }

    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(390, 844)]
    [InlineData(3440, 1440)]
    public void FullscreenCameraCoversViewportAndKeepsAimAligned(int width, int height)
    {
        foreach (var mode in Enum.GetValues<MapMode>())
        {
            var map = MapCatalog.FixedMaps.First(m => m.Mode == mode);
            var camera = MapCamera.For(map, map.Width / 2.0, map.Height / 2.0, width, height);
            Assert.True(camera.X >= 0 && camera.Y >= 0);
            Assert.True(camera.X + width / camera.Scale <= map.Width + .001);
            Assert.True(camera.Y + height / camera.Scale <= map.Height + .001);
            Assert.Equal((map.Width / 2, map.Height / 2), camera.ToWorld(width / 2.0, height / 2.0));
            var edge = MapCamera.For(map, map.Width, map.Height, width, height);
            Assert.Equal((map.Width, map.Height), edge.ToWorld(width, height));
        }
        var big = MapCatalog.FixedMaps.First(m => m.Mode == MapMode.BigMap);
        var fog = MapCatalog.FixedMaps.First(m => m.Mode == MapMode.Foggish);
        Assert.Equal(MapCamera.For(big, 0, 0, width, height).Scale,
            MapCamera.For(fog, 0, 0, width, height).Scale);
    }

    [Fact]
    public void SpectatingFollowsLivingPlayerAndReturnsToLocalTankAfterRespawn()
    {
        var local = new TankState { Id = Guid.NewGuid(), Health = 0 };
        var other = new TankState { Id = Guid.NewGuid(), Health = 3 };
        var third = new TankState { Id = Guid.NewGuid(), Health = 3 };
        Assert.Equal(other, MapCamera.FollowTarget([local, other, third], local.Id, null));
        Assert.Equal(third, MapCamera.FollowTarget([local, other, third], local.Id, third.Id));
        Assert.Equal(other, MapCamera.FollowTarget([local with { Eliminated = true }, other], local.Id, null));
        var revived = local with { Health = 3 };
        Assert.Equal(revived, MapCamera.FollowTarget([revived, other], local.Id, other.Id));
        Assert.Null(MapCamera.FollowTarget([local, other with { Health = 0 }], local.Id, other.Id));
    }

    [Theory]
    [InlineData(1800, 1400, 900, 700)]
    [InlineData(2400, 1400, 1000, 700)]
    [InlineData(2250, 1750, 900, 700)]
    [InlineData(1800, 1400, 1080, 840)]
    public void BigMapPresetsRescaleEveryLayoutWithValidSpawns(int width, int height, int viewWidth, int viewHeight)
    {
        var source = new JsonMapSource(new Dictionary<MapMode, ModeSettings>
        {
            [MapMode.BigMap] = new(width, height, viewWidth, viewHeight, 20, 27)
        });
        foreach (var map in source.Maps.Where(m => m.Mode == MapMode.BigMap))
        {
            Assert.Equal((width, height), (map.Width, map.Height));
            Assert.All(map.SpawnPoints, p => Assert.False(map.Blocks(new(p.X, p.Y - 26, 60, 60))));
        }
    }
}
