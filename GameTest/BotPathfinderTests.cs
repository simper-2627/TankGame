using GameLogic;
using GameLogic.Bots;

namespace GameTest;

public class BotPathfinderTests
{
    private static readonly DeveloperGameSettings Dev = new();

    // An 800x400 map split by a wall at x 380-420 with one opening of `gap` px centered on y = 200
    private static GameMap WallWithGap(int gap) => new("Gap", 800, 400,
        [new Obstacle(380, 0, 40, 200 - gap / 2), new Obstacle(380, 200 + gap / 2, 40, 200 - gap / 2)], []);

    private static void AssertEverySegmentIsClear(GameMap map, (double X, double Y) from, IReadOnlyList<(double X, double Y)> path)
    {
        var at = from;
        foreach (var point in path)
        {
            Assert.True(BotPathfinder.IsClear(map, at, point, Dev), $"segment {at} -> {point} touches a wall");
            at = point;
        }
    }

    [Fact]
    public void OnAnOpenMapThePathIsAStraightShot()
    {
        var path = BotPathfinder.FindPath(BotViews.Open, (100, 200), (700, 200), Dev);

        Assert.NotNull(path);
        Assert.InRange(path.Count, 1, 2);
        Assert.True(BotSenses.Distance(path[^1], (700, 200)) <= BotPathfinder.CellSize);
        Assert.True(BotPathfinder.IsClear(BotViews.Open, (100, 200), (700, 200), Dev));
    }

    [Fact]
    public void ThePathGoesThroughTheGapInAWall()
    {
        var map = WallWithGap(80);

        var path = BotPathfinder.FindPath(map, (100, 60), (700, 60), Dev);

        Assert.NotNull(path);
        Assert.Contains(path, point => point.X > 340 && point.X < 460 && point.Y > 160 && point.Y < 240);
        AssertEverySegmentIsClear(map, (100, 60), path);
        Assert.True(BotSenses.Distance(path[^1], (700, 60)) <= BotPathfinder.CellSize);
    }

    [Fact]
    public void AGoalWalledInOnAllSidesHasNoPath()
    {
        // A closed pen around (650, 200), roomy enough inside for a tank
        var map = new GameMap("Pen", 800, 400,
        [
            new Obstacle(560, 110, 180, 10), new Obstacle(560, 280, 180, 10),
            new Obstacle(560, 110, 10, 180), new Obstacle(730, 110, 10, 180),
        ], []);

        Assert.Null(BotPathfinder.FindPath(map, (100, 200), (650, 200), Dev));
    }

    [Fact]
    public void AStartInsideAWallSnapsToTheNearestFreeSpot()
    {
        var map = WallWithGap(80);

        var path = BotPathfinder.FindPath(map, (400, 60), (700, 60), Dev);

        Assert.NotNull(path);
        Assert.True(BotSenses.Distance(path[^1], (700, 60)) <= BotPathfinder.CellSize);
    }

    [Theory]
    [InlineData(30, false)]
    [InlineData(80, true)]
    public void IsClearOnlyThroughAGapATankFitsIn(int gap, bool clear)
    {
        Assert.Equal(clear, BotPathfinder.IsClear(WallWithGap(gap), (100, 200), (700, 200), Dev));
    }

    [Fact]
    public void IsClearIsFalseAcrossAWall()
    {
        Assert.False(BotPathfinder.IsClear(BotViews.Walled, (100, 200), (700, 200), Dev));
    }

    [Fact]
    public void ThePathNeverLeavesTheMap()
    {
        var map = WallWithGap(80);

        var path = BotPathfinder.FindPath(map, (40, 30), (770, 380), Dev);

        Assert.NotNull(path);
        var half = Tank.Size / 2.0 - Dev.HitboxInset;
        Assert.All(path, point =>
        {
            Assert.InRange(point.X, half, map.Width - half);
            Assert.InRange(point.Y, half, map.Height - half);
        });
    }
}
