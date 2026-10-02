using GameLogic.Bots;

namespace GameTest;

public class BotSteeringTests
{
    [Theory]
    [InlineData(1, 0, false, false, false, true)]
    [InlineData(1, 1, false, true, false, true)]
    [InlineData(0, 1, false, true, false, false)]
    [InlineData(-1, 1, false, true, true, false)]
    [InlineData(-1, 0, false, false, true, false)]
    [InlineData(-1, -1, true, false, true, false)]
    [InlineData(0, -1, true, false, false, false)]
    [InlineData(1, -1, true, false, false, true)]
    [InlineData(10, 1, false, false, false, true)]
    [InlineData(-300, 0, false, false, true, false)]
    public void SteersWithTheClosestOfTheEightKeyCombinations(double dx, double dy, bool up, bool down, bool left, bool right)
    {
        Assert.Equal(new Keys(up, down, left, right), BotSteering.Toward(dx, dy));
    }

    [Fact]
    public void NoDirectionPressesNothing()
    {
        Assert.Equal(Keys.None, BotSteering.Toward(0, 0));
        Assert.False(Keys.None.Any);
        Assert.True(BotSteering.Toward(1, 0).Any);
    }

    [Fact]
    public void NeverStuckWhenNoKeyIsPressed()
    {
        var detector = new StuckDetector();

        for (var tick = 0; tick < 30; tick++)
            Assert.False(detector.Update(100, 100, pressedMove: false));
    }

    [Fact]
    public void StuckAfterEightTicksOfPushingWithoutMoving()
    {
        var detector = new StuckDetector();

        for (var tick = 1; tick < StuckDetector.Window; tick++)
            Assert.False(detector.Update(100, 100, pressedMove: true));
        Assert.True(detector.Update(100, 100, pressedMove: true));
    }

    [Fact]
    public void NotStuckWhileTheTankKeepsMoving()
    {
        var detector = new StuckDetector();

        for (var tick = 0; tick < 40; tick++)
            Assert.False(detector.Update(100 + tick * 8, 100, pressedMove: true));
    }

    [Fact]
    public void LettingGoOfTheKeysStartsTheCountAgain()
    {
        var detector = new StuckDetector();
        for (var tick = 0; tick < 5; tick++)
            detector.Update(100, 100, pressedMove: true);
        detector.Update(100, 100, pressedMove: false);

        for (var tick = 1; tick < StuckDetector.Window; tick++)
            Assert.False(detector.Update(100, 100, pressedMove: true));
    }

    [Fact]
    public void ADetectedStuckResetsSoItDoesNotFireEveryTick()
    {
        var detector = new StuckDetector();
        for (var tick = 0; tick < StuckDetector.Window; tick++)
            detector.Update(100, 100, pressedMove: true);

        Assert.False(detector.Update(100, 100, pressedMove: true));
    }
}
