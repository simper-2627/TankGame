using GameLogic;

namespace GameTest;

public class HitFlashTests
{
    private static Tank TankAt(int x, int health = 3) => new() { PositionX = x, PositionY = 200, Health = health };

    [Fact]
    public void SurvivingAHitStartsTheFlash()
    {
        var shooter = TankAt(100);
        var target = TankAt(400, health: 3);
        var tanks = new List<Tank> { shooter, target };

        Combat.ApplyHit(tanks, 1, shooter.Id, new MatchSettings());

        Assert.Equal(Tank.HitFlashTicksOnHit, tanks[1].HitFlashTicks);
        Assert.Equal(0, tanks[0].HitFlashTicks);
    }

    [Fact]
    public async Task FlashCountsDownEveryTickThenStops()
    {
        var game = TestGames.NewGame();
        var id = game.JoinGame();
        game.JoinGame();
        game.Tanks = game.Tanks.Select(t => t.Id == id ? t with { HitFlashTicks = 2 } : t).ToArray();

        await game.loopRunner.ProcessGameTick();
        Assert.Equal(1, game.Tanks.Single(t => t.Id == id).HitFlashTicks);
        await game.loopRunner.ProcessGameTick();
        Assert.Equal(0, game.Tanks.Single(t => t.Id == id).HitFlashTicks);
        await game.loopRunner.ProcessGameTick();
        Assert.Equal(0, game.Tanks.Single(t => t.Id == id).HitFlashTicks);
    }

    [Fact]
    public void EveryoneSeesTheFlashButNotTheHealth()
    {
        var game = TestGames.NewGame(new MatchSettings { Health = 5 });
        var me = game.JoinGame();
        var enemy = game.JoinGame();
        game.Tanks = game.Tanks.Select(t => t.Id == me ? t with { Health = 4, HitFlashTicks = 2 } : t).ToArray();

        var asSeenByEnemy = game.GetGameState(viewerId: enemy).Tanks!.Single(t => t.Id == me);

        Assert.True(asSeenByEnemy.Flashing);
        Assert.Null(asSeenByEnemy.Health);
    }
}
