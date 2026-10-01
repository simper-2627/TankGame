using GameLogic;
using GameLogic.Game;

namespace GameTest;

// Hand-built views, so a brain test controls exactly what the bot sees
internal static class BotViews
{
    public static readonly GameMap Open = TestGames.Arena;
    // A wall across the middle of the arena, between x = 380 and x = 420
    public static readonly GameMap Walled = new("Walled", 800, 400, [new Obstacle(380, 0, 40, 400)], []);
    // The same wall with an 80 px opening between y = 160 and y = 240
    public static readonly GameMap WalledWithGap = new("WalledWithGap", 800, 400,
        [new Obstacle(380, 0, 40, 160), new Obstacle(380, 240, 40, 160)], []);

    // The bot itself: it can see its own health and reload
    public static TankState Me(Guid id, int x, int y, int reloadMsLeft = 0, int health = 3) => new()
    {
        Id = id, PositionX = x, PositionY = y, IsBot = true, Health = health, ReloadMsLeft = reloadMsLeft,
    };

    // Everyone else: health and reload stay hidden
    public static TankState Human(Guid id, int x, int y, bool eliminated = false) => new()
    {
        Id = id, PositionX = x, PositionY = y, Eliminated = eliminated,
    };

    public static GameState View(BotDifficulty difficulty, IEnumerable<TankState> tanks,
        IEnumerable<BulletState>? bullets = null, ProjectileType projectile = ProjectileType.DumbBubbles) => new()
    {
        Name = "test",
        Status = GameStatus.Playing,
        Settings = new MatchSettings { BotDifficulty = difficulty, Projectile = projectile },
        Tanks = tanks.ToArray(),
        Bullets = (bullets ?? []).ToArray(),
    };
}
