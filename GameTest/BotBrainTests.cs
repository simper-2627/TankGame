using GameLogic;
using GameLogic.Bots;

namespace GameTest;

public class BotBrainTests
{
    private static readonly Guid MeId = Guid.NewGuid();
    private static readonly Guid HumanId = Guid.NewGuid();

    private static BotBrain NewBrain(int seed = 1) => new(MeId, new Random(seed));

    // The bot on the left and the human to the right on the same row, so the target is straight ahead.
    // Tank centers are 30 px right of their x, so humanX 600 is 500 px away
    private static GameState Duel(BotDifficulty difficulty, int humanX, int reloadMsLeft = 0,
        ProjectileType projectile = ProjectileType.DumbBubbles) =>
        BotViews.View(difficulty, [BotViews.Me(MeId, 100, 200, reloadMsLeft), BotViews.Human(HumanId, humanX, 200)],
            projectile: projectile);

    [Fact]
    public void SeeksTheHumanUntilItHasHadAClearViewForTheReactionDelay()
    {
        var brain = NewBrain();

        for (var tick = 1; tick < 6; tick++)
        {
            var input = brain.Decide(Duel(BotDifficulty.Easy, 600), BotViews.Open);
            Assert.Equal(BotState.Seek, brain.State);
            Assert.True(input.Right);
            Assert.False(input.Shoot);
        }
        brain.Decide(Duel(BotDifficulty.Easy, 600), BotViews.Open);

        Assert.Equal(BotState.Attack, brain.State);
    }

    [Theory]
    [InlineData(600, true, false)]
    [InlineData(300, false, true)]
    public void AttackClosesInWhenFarAndBacksOffWhenTooClose(int humanX, bool right, bool left)
    {
        var brain = NewBrain();

        var input = brain.Decide(Duel(BotDifficulty.Hard, humanX), BotViews.Open);

        Assert.Equal(BotState.Attack, brain.State);
        Assert.Equal(right, input.Right);
        Assert.Equal(left, input.Left);
    }

    [Fact]
    public void AttackStrafesSidewaysWhenInRange()
    {
        var brain = NewBrain();

        for (var tick = 0; tick < 40; tick++)
        {
            var input = brain.Decide(Duel(BotDifficulty.Hard, 420), BotViews.Open);
            Assert.False(input.Left || input.Right);
            Assert.True(input.Up || input.Down);
        }
    }

    [Fact]
    public void ShootIsPressedOneTickAndReleasedTheNext()
    {
        var brain = NewBrain();

        var presses = Enumerable.Range(0, 4)
            .Select(_ => brain.Decide(Duel(BotDifficulty.Hard, 420), BotViews.Open).Shoot)
            .ToArray();

        Assert.Equal([true, false, true, false], presses);
    }

    [Fact]
    public void HoldsFireWhileReloading()
    {
        var brain = NewBrain();

        for (var tick = 0; tick < 5; tick++)
            Assert.False(brain.Decide(Duel(BotDifficulty.Hard, 420, reloadMsLeft: 500), BotViews.Open).Shoot);
    }

    [Fact]
    public void NeverShootsWithoutLineOfSight()
    {
        var brain = NewBrain();

        for (var tick = 0; tick < 10; tick++)
            Assert.False(brain.Decide(Duel(BotDifficulty.Hard, 600), BotViews.Walled).Shoot);
        Assert.Equal(BotState.Seek, brain.State);
    }

    [Fact]
    public void AttackGoesBackToSeekOnlyAfterFiveTicksWithoutLineOfSight()
    {
        var brain = NewBrain();
        brain.Decide(Duel(BotDifficulty.Hard, 600), BotViews.Open);
        Assert.Equal(BotState.Attack, brain.State);

        for (var tick = 1; tick < BotBrain.LostSightTicks; tick++)
        {
            brain.Decide(Duel(BotDifficulty.Hard, 600), BotViews.Walled);
            Assert.Equal(BotState.Attack, brain.State);
        }
        brain.Decide(Duel(BotDifficulty.Hard, 600), BotViews.Walled);

        Assert.Equal(BotState.Seek, brain.State);
    }

    [Fact]
    public void OnlyBotsAroundMeansNothingToDo()
    {
        var brain = NewBrain();
        var view = BotViews.View(BotDifficulty.Hard,
            [BotViews.Me(MeId, 100, 200), BotViews.Me(Guid.NewGuid(), 300, 200)]);

        var input = brain.Decide(view, BotViews.Open);

        Assert.Equal(BotState.Inactive, brain.State);
        Assert.False(input.Up || input.Down || input.Left || input.Right || input.Shoot);
    }

    [Fact]
    public void TargetsTheHumanEvenWhenAnotherBotIsCloser()
    {
        var brain = NewBrain();
        var view = BotViews.View(BotDifficulty.Hard,
            [BotViews.Me(MeId, 100, 200), BotViews.Me(Guid.NewGuid(), 300, 200), BotViews.Human(HumanId, 600, 200)]);

        var input = brain.Decide(view, BotViews.Open);

        // The human's center is 630 px across; the turret points there, not at the bot at 330
        Assert.InRange(input.AimX!.Value, 600, 640);
    }

    [Fact]
    public void ADeadBotDoesNothingAndResumesWhenItRespawns()
    {
        var brain = NewBrain();
        var dead = BotViews.View(BotDifficulty.Hard,
            [BotViews.Me(MeId, 100, 200, health: 0), BotViews.Human(HumanId, 600, 200)]);

        var input = brain.Decide(dead, BotViews.Open);
        Assert.Equal(BotState.Inactive, brain.State);
        Assert.False(input.Up || input.Down || input.Left || input.Right || input.Shoot);

        brain.Decide(Duel(BotDifficulty.Hard, 600), BotViews.Open);
        Assert.NotEqual(BotState.Inactive, brain.State);
    }

    [Fact]
    public void WhenTheHumanIsEliminatedTheBotStopsAndPressesNothing()
    {
        var brain = NewBrain();
        brain.Decide(Duel(BotDifficulty.Hard, 420), BotViews.Open);
        var view = BotViews.View(BotDifficulty.Hard,
            [BotViews.Me(MeId, 100, 200), BotViews.Human(HumanId, 420, 200, eliminated: true)]);

        var input = brain.Decide(view, BotViews.Open);

        Assert.Equal(BotState.Inactive, brain.State);
        Assert.False(input.Up || input.Down || input.Left || input.Right || input.Shoot);
    }

    [Fact]
    public void AMatchThatHasEndedMeansNothingToDo()
    {
        var brain = NewBrain();
        var view = Duel(BotDifficulty.Hard, 420) with { Status = GameLogic.Game.GameStatus.Ended };

        var input = brain.Decide(view, BotViews.Open);

        Assert.Equal(BotState.Inactive, brain.State);
        Assert.False(input.Shoot);
    }

    [Theory]
    [InlineData(BotDifficulty.Easy, 15.5)]
    [InlineData(BotDifficulty.Medium, 6.5)]
    [InlineData(BotDifficulty.Hard, 1.5)]
    public void AimErrorStaysWithinTheLimitForTheDifficulty(BotDifficulty difficulty, double limitDegrees)
    {
        var angles = new List<double>();
        for (var seed = 0; seed < 50; seed++)
        {
            var input = NewBrain(seed).Decide(Duel(difficulty, 420), BotViews.Open);
            // Bot and human share a row, so a perfect aim is exactly 0 degrees
            angles.Add(Math.Atan2(input.AimY!.Value - 204, input.AimX!.Value - 130) * 180 / Math.PI);
        }

        Assert.All(angles, angle => Assert.InRange(angle, -limitDegrees, limitDegrees));
        if (difficulty != BotDifficulty.Hard)
            Assert.Contains(angles, angle => Math.Abs(angle) > 1);
    }

    [Fact]
    public void ChangingTheDifficultyMidMatchTakesEffectOnTheNextTick()
    {
        var brain = NewBrain();

        brain.Decide(Duel(BotDifficulty.Easy, 600), BotViews.Open);
        Assert.Equal(BotState.Seek, brain.State);

        brain.Decide(Duel(BotDifficulty.Hard, 600), BotViews.Open);
        Assert.Equal(BotState.Attack, brain.State);
    }

    [Fact]
    public void HardLeadsAMovingHumanButNotWithInstantShots()
    {
        var leading = NewBrain();
        leading.Decide(Duel(BotDifficulty.Hard, 420), BotViews.Open);
        var movedOn = leading.Decide(Duel(BotDifficulty.Hard, 430), BotViews.Open);

        // The human moved 10 px/tick: a bullet that takes ~16 ticks to arrive needs aiming well ahead of its center at 460
        Assert.True(movedOn.AimX > 560);

        var instant = NewBrain();
        instant.Decide(Duel(BotDifficulty.Hard, 420, projectile: ProjectileType.Realistic), BotViews.Open);
        var noLead = instant.Decide(Duel(BotDifficulty.Hard, 430, projectile: ProjectileType.Realistic), BotViews.Open);

        Assert.InRange(noLead.AimX!.Value, 440, 480);
    }
}
