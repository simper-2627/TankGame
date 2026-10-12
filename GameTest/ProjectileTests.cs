using GameLogic;
using GameLogic.Game;

namespace GameTest;

public class ProjectileTests
{
    private static readonly DeveloperGameSettings Dev = new();

    // Arena: spawn 0 at (100,200) facing right, spawn 1 at (400,200) facing left, same row
    private static (Game Game, Guid A, Guid B) Duel(MatchSettings settings)
    {
        var game = TestGames.NewGame(settings);
        var a = game.JoinGame();
        var b = game.JoinGame();
        return (game, a, b);
    }

    [Fact]
    public void BubbleSpeedIsSanitizedToAnAllowedChoice()
    {
        Assert.Equal(20, MatchSettings.Sanitize(new MatchSettings { BulletSpeed = 21 }).BulletSpeed);
        Assert.Equal(40, MatchSettings.Sanitize(new MatchSettings { BulletSpeed = 9999 }).BulletSpeed);
        Assert.Equal(5, MatchSettings.Sanitize(new MatchSettings { BulletSpeed = int.MinValue }).BulletSpeed);
    }

    [Fact]
    public void UnknownProjectileTypeFallsBackToBubbles()
    {
        var sanitized = MatchSettings.Sanitize(new MatchSettings { Projectile = (ProjectileType)99 });

        Assert.Equal(ProjectileType.DumbBubbles, sanitized.Projectile);
    }

    [Fact]
    public void DumbBubblesFlyAtTheConfiguredSpeed()
    {
        var (game, a, b) = Duel(new MatchSettings { BulletSpeed = 10 });
        TestGames.ShootAt(game, a, b);

        var bullet = game.Bullets.Single();

        Assert.Equal(10, bullet.Speed);
        Assert.Empty(game.Explosions);
    }

    [Fact]
    public async Task FasterBubblesCoverMoreGroundPerTick()
    {
        double Travelled(int speed)
        {
            var (game, a, b) = Duel(new MatchSettings { BulletSpeed = speed });
            TestGames.ShootAt(game, a, b);
            var start = game.Bullets.Single();
            game.loopRunner.ProcessGameTick().GetAwaiter().GetResult();
            var moved = game.Bullets.Single();
            return Math.Abs(moved.PositionX - start.PositionX);
        }

        await Task.CompletedTask;
        Assert.True(Travelled(30) > Travelled(10));
    }

    [Fact]
    public void RealisticShotHitsInstantlyWithoutAFlyingBullet()
    {
        var (game, a, b) = Duel(new MatchSettings { Projectile = ProjectileType.Realistic, Health = 3 });

        TestGames.ShootAt(game, a, b);

        Assert.Empty(game.Bullets);
        Assert.Equal(2, game.Tanks.Single(t => t.Id == b).Health);
        Assert.Equal(1, game.Tanks.Single(t => t.Id == a).HitsLanded);
        var blast = Assert.Single(game.Explosions);
        var area = Tank.GetCollisionArea(game.Tanks.Single(t => t.Id == b), game.DeveloperSettings);
        Assert.InRange(blast.X, area.X - 4, area.X + area.Width + 4);
        Assert.InRange(blast.Y, area.Y - 4, area.Y + area.Height + 4);
    }

    [Fact]
    public void RealisticShotStopsAtAWallAndExplodesThere()
    {
        var map = new GameMap("Walled", 800, 400, [new Obstacle(250, 150, 30, 100)],
            [new MapSpawnPoint(100, 200, 0), new MapSpawnPoint(400, 200, 180)]);
        var game = new Game(new FakeHubContext()) { Map = map, SpawnRandom = new FirstSpawnRandom(), Settings = new MatchSettings { Projectile = ProjectileType.Realistic } };
        var a = game.JoinGame();
        var b = game.JoinGame();

        TestGames.ShootAt(game, a, b);

        Assert.Equal(3, game.Tanks.Single(t => t.Id == b).Health);
        var blast = Assert.Single(game.Explosions);
        Assert.InRange(blast.X, 246, 254);
    }

    [Fact]
    public void RealisticShotNeverHitsItsOwnShooter()
    {
        var (game, a, _) = Duel(new MatchSettings { Projectile = ProjectileType.Realistic });
        var shooter = game.Tanks.Single(t => t.Id == a);

        var shot = Combat.TraceShot(shooter with { TurretAngle = 180 }, game.Tanks.ToList(), game.Map, Dev);

        Assert.Null(shot.HitIndex);
    }

    [Fact]
    public void RealisticShotDoesNoDamageBeforeTheSecondPlayerJoins()
    {
        var game = TestGames.NewGame(new MatchSettings { Projectile = ProjectileType.Realistic });
        var a = game.JoinGame();
        game.ReceiveUserInput(TestGames.Input(a, shoot: true, aimX: 700, aimY: 200));

        Assert.Single(game.Explosions);
    }

    [Fact]
    public void ReloadStillAppliesToRealisticShots()
    {
        var (game, a, b) = Duel(new MatchSettings { Projectile = ProjectileType.Realistic, ReloadMs = 1000 });
        TestGames.ShootAt(game, a, b);
        TestGames.ShootAt(game, a, b);

        Assert.Equal(2, game.Tanks.Single(t => t.Id == b).Health);
    }

    [Fact]
    public async Task ExplosionsFadeAwayAfterTheirAnimation()
    {
        var (game, a, b) = Duel(new MatchSettings { Projectile = ProjectileType.Realistic });
        TestGames.ShootAt(game, a, b);
        Assert.Equal(0, game.GetGameState().Explosions!.Single().Age);

        await game.loopRunner.ProcessGameTick();
        Assert.Equal(1, game.GetGameState().Explosions!.Single().Age);

        await TestGames.TickUntil(game, () => !game.Explosions.Any(), Explosion.Ticks + 2);
    }

    [Fact]
    public void ExplosionRemembersTheMuzzleForTheTracerLine()
    {
        var (game, a, b) = Duel(new MatchSettings { Projectile = ProjectileType.Realistic });
        var shooter = game.Tanks.Single(t => t.Id == a);
        var (centerX, centerY) = Tank.GetCenter(shooter, game.DeveloperSettings);

        TestGames.ShootAt(game, a, b);

        var blast = game.GetGameState().Explosions!.Single();
        // The barrel is Tank.BarrelLength long, pointing at the target (to the right along the row)
        Assert.InRange(blast.FromX, centerX + Tank.BarrelLength - 3, centerX + Tank.BarrelLength + 3);
        Assert.InRange(blast.FromY, centerY - 3, centerY + 3);
        Assert.True(blast.X > blast.FromX);
    }

    [Fact]
    public void ShieldedTankTakesNoDamageFromADirectHit()
    {
        var shooter = new Tank();
        var shielded = new Tank { Health = 3, ShieldTicksLeft = Tank.ShieldDurationTicks };
        var tanks = new List<Tank> { shooter, shielded };

        Combat.ApplyHit(tanks, 1, shooter.Id, new MatchSettings());

        Assert.Equal(3, tanks[1].Health);
        Assert.Equal(0, tanks[1].HitFlashTicks);
        Assert.Equal(0, tanks[0].HitsLanded);
        Assert.True(tanks[1].Shielded);
    }

    [Fact]
    public void BulletIsStillConsumedByAShieldedTank()
    {
        var shielded = new Tank { Health = 3, ShieldTicksLeft = Tank.ShieldDurationTicks };
        var area = Tank.GetCollisionArea(shielded, Dev);
        var bullet = new Bullet { PositionX = area.X, PositionY = area.Y, Angle = 0, OwnerId = Guid.NewGuid() };

        var (tanks, bullets) = Combat.ResolveHits(new[] { shielded }, new[] { bullet }, Dev);

        Assert.Empty(bullets);
        Assert.Equal(3, tanks[0].Health);
    }
}
