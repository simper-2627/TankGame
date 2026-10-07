using GameLogic.Bots;

namespace GameTest;

public class BotAimTests
{
    private static double AngleOf((double X, double Y) from, (int X, int Y) aim) =>
        Math.Atan2(aim.Y - from.Y, aim.X - from.X) * 180 / Math.PI;

    [Fact]
    public void AimsAtTheTargetWithNoLeadAndNoError()
    {
        var aim = BotAim.AimPoint((100, 100), (500, 100), (0, 0), 20, lead: 0, errorDegrees: 0);

        Assert.Equal((500, 100), aim);
    }

    [Fact]
    public void LeadsAMovingTargetByHowFarItTravelsWhileTheBulletFlies()
    {
        // 400 px away at 20 px/tick is 20 ticks; the target moves 10 px/tick to the right, so 200 px ahead
        var aim = BotAim.AimPoint((100, 100), (500, 100), (10, 0), 20, lead: 1, errorDegrees: 0);

        Assert.Equal((700, 100), aim);
    }

    [Fact]
    public void ALeadOfHalfAimsHalfwayThere()
    {
        var aim = BotAim.AimPoint((100, 100), (500, 100), (10, 0), 20, lead: 0.5, errorDegrees: 0);

        Assert.Equal((600, 100), aim);
    }

    [Fact]
    public void NoLeadIgnoresTheTargetsMovement()
    {
        var aim = BotAim.AimPoint((100, 100), (500, 100), (10, 0), 20, lead: 0, errorDegrees: 0);

        Assert.Equal((500, 100), aim);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(-15)]
    [InlineData(6)]
    [InlineData(1)]
    public void ErrorTurnsTheAimByThatManyDegrees(double error)
    {
        var from = (X: 100.0, Y: 100.0);

        var aim = BotAim.AimPoint(from, (500, 100), (0, 0), 20, lead: 0, errorDegrees: error);

        Assert.InRange(AngleOf(from, aim), error - 0.3, error + 0.3);
    }

    [Fact]
    public void AnAimOnTopOfTheBotStillPointsSomewhere()
    {
        var aim = BotAim.AimPoint((100, 100), (100, 100), (0, 0), 20, lead: 0, errorDegrees: 0);

        // Same spot would give the turret no direction at all
        Assert.NotEqual((100, 100), aim);
    }
}
