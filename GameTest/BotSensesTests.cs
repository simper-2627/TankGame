using GameLogic;
using GameLogic.Bots;

namespace GameTest;

public class BotSensesTests
{
    private static readonly DeveloperGameSettings Dev = new();
    private static readonly GameMap Open = new("Open", 800, 400, [], []);
    private static readonly GameMap Walled = new("Walled", 800, 400, [new Obstacle(380, 0, 40, 400)], []);

    private static TankState At(int x, int y) => new() { Id = Guid.NewGuid(), PositionX = x, PositionY = y };

    [Fact]
    public void CenterIsTheMiddleOfTheDrawnTank()
    {
        var (x, y) = BotSenses.Center(At(100, 200), Dev);

        Assert.Equal(130, x);
        // The sprite sits VisualTopOffset (26) above the tank's anchor
        Assert.Equal(200 - 26 + 30, y);
    }

    [Fact]
    public void LineOfSightIsClearInTheOpen()
    {
        Assert.True(BotSenses.HasLineOfSight(Open, (130, 204), (700, 204)));
    }

    [Fact]
    public void AWallBetweenTheTanksBlocksLineOfSight()
    {
        Assert.False(BotSenses.HasLineOfSight(Walled, (130, 204), (700, 204)));
    }

    [Fact]
    public void TanksOnTheSameSideOfAWallSeeEachOther()
    {
        Assert.True(BotSenses.HasLineOfSight(Walled, (130, 204), (300, 204)));
    }

    [Fact]
    public void ABulletHeadingStraightAtATankIsAThreat()
    {
        // Bullet top-left (0, 200) is centered on (5, 205), flying right along y = 205
        var bullet = new BulletState { Id = Guid.NewGuid(), PositionX = 0, PositionY = 200, Angle = 0 };

        var dodge = BotSenses.Threat(bullet, (100, 205), 20);

        Assert.NotNull(dodge);
        // Sidestep across the bullet's path, not along it
        Assert.InRange(dodge!.Value.X, -0.001, 0.001);
        Assert.NotEqual(0, dodge.Value.Y);
    }

    [Fact]
    public void ADodgeGoesAwayFromTheBulletsLine()
    {
        var bullet = new BulletState { Id = Guid.NewGuid(), PositionX = 0, PositionY = 200, Angle = 0 };

        // The tank is a little above the bullet's line (smaller y), so it should dodge up
        var dodge = BotSenses.Threat(bullet, (100, 180), 20);

        Assert.True(dodge!.Value.Y < 0);
    }

    [Fact]
    public void ABulletTooFarAwayToArriveSoonIsNotAThreatYet()
    {
        var bullet = new BulletState { Id = Guid.NewGuid(), PositionX = 0, PositionY = 200, Angle = 0 };

        Assert.Null(BotSenses.Threat(bullet, (400, 205), 20));
    }

    [Fact]
    public void ABulletFlyingAwayIsNotAThreat()
    {
        var bullet = new BulletState { Id = Guid.NewGuid(), PositionX = 300, PositionY = 200, Angle = 0 };

        Assert.Null(BotSenses.Threat(bullet, (100, 205), 20));
    }

    [Fact]
    public void ABulletPassingWideIsNotAThreat()
    {
        var bullet = new BulletState { Id = Guid.NewGuid(), PositionX = 0, PositionY = 200, Angle = 0 };

        Assert.Null(BotSenses.Threat(bullet, (100, 305), 20));
    }

    // Distance to a target is just its X here, which keeps the numbers readable
    private static double Far(TankState tank) => tank.PositionX;

    [Fact]
    public void TheClosestHumanIsChosenFirst()
    {
        var near = At(200, 0);
        var far = At(300, 0);

        var chosen = new TargetTracker().Choose([far, near], Far);

        Assert.Equal(near.Id, chosen!.Id);
    }

    [Fact]
    public void NoHumansMeansNoTarget()
    {
        Assert.Null(new TargetTracker().Choose([], Far));
    }

    [Fact]
    public void ASlightlyCloserHumanDoesNotStealTheTarget()
    {
        var tracker = new TargetTracker();
        var first = At(300, 0);
        tracker.Choose([first], Far);
        var slightlyCloser = At(280, 0);

        for (var tick = 0; tick < 30; tick++)
            Assert.Equal(first.Id, tracker.Choose([first, slightlyCloser], Far)!.Id);
    }

    [Fact]
    public void AMuchCloserHumanTakesOverAfterTenTicks()
    {
        var tracker = new TargetTracker();
        var first = At(300, 0);
        tracker.Choose([first], Far);
        var muchCloser = At(200, 0);

        for (var tick = 1; tick < TargetTracker.SwitchTicks; tick++)
            Assert.Equal(first.Id, tracker.Choose([first, muchCloser], Far)!.Id);
        Assert.Equal(muchCloser.Id, tracker.Choose([first, muchCloser], Far)!.Id);
    }

    [Fact]
    public void ALapsedChallengeStartsCountingFromScratch()
    {
        var tracker = new TargetTracker();
        var first = At(300, 0);
        tracker.Choose([first], Far);
        var closer = At(200, 0);
        var notCloserNow = At(290, 0);

        for (var tick = 0; tick < 6; tick++)
            tracker.Choose([first, closer], Far);
        tracker.Choose([first, notCloserNow], Far);

        // Six more ticks of being close is still not ten in a row
        for (var tick = 0; tick < 6; tick++)
            Assert.Equal(first.Id, tracker.Choose([first, closer], Far)!.Id);
    }

    [Fact]
    public void ALostTargetIsReplacedAtOnce()
    {
        var tracker = new TargetTracker();
        var first = At(300, 0);
        var second = At(500, 0);
        tracker.Choose([first, second], Far);

        Assert.Equal(second.Id, tracker.Choose([second], Far)!.Id);
    }
}
