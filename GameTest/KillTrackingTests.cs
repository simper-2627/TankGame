using GameLogic;

namespace GameTest;

public class KillTrackingTests
{
    private static (List<Tank> Tanks, Tank Shooter) Duel(int targetHealth)
    {
        var shooter = new Tank();
        var target = new Tank { Health = targetHealth };
        return ([shooter, target], shooter);
    }

    [Fact]
    public void DestroyingATankCountsAKillAndAHit()
    {
        var (tanks, shooter) = Duel(targetHealth: 1);

        Combat.ApplyHit(tanks, 1, shooter.Id, new MatchSettings());

        Assert.Equal(1, tanks[0].Kills);
        Assert.Equal(1, tanks[0].HitsLanded);
    }

    [Fact]
    public void AHitThatLeavesTheTargetAliveIsNotAKill()
    {
        var (tanks, shooter) = Duel(targetHealth: 2);

        Combat.ApplyHit(tanks, 1, shooter.Id, new MatchSettings());

        Assert.Equal(0, tanks[0].Kills);
        Assert.Equal(1, tanks[0].HitsLanded);
    }

    [Fact]
    public void ShootingYourselfIsNeitherAHitNorAKill()
    {
        var solo = new Tank { Health = 1 };
        var tanks = new List<Tank> { solo };

        Combat.ApplyHit(tanks, 0, solo.Id, new MatchSettings());

        Assert.Equal(0, tanks[0].Kills);
        Assert.Equal(0, tanks[0].HitsLanded);
    }
}
