namespace GameLogic;

public static class ProjectileTypeExtensions
{
    public static string Label(this ProjectileType type) => type switch
    {
        ProjectileType.Realistic => "Realistic (instant hit)",
        _ => "Dumb bubbles",
    };
}

public enum ProjectileType
{
    // Slow visible bubbles that fly across the board and can bounce; speed is configurable
    DumbBubbles,
    // Instant hit at realistic tank-shell speed, with a small explosion where it lands
    Realistic,
}

public enum GameMode
{
    FreeForAll,
    TeamElimination,
}

public static class GameModeExtensions
{
    public static string Label(this GameMode mode) => mode switch
    {
        GameMode.TeamElimination => "Teams",
        _ => "Free for all",
    };
}

public static class Teams
{
    // Teams are numbered 1 and 2; the name doubles as the tank sprite colour. Red is left for bots
    public static string Name(int team) => team == 1 ? "Blue" : "Green";
}

// Match rules the creator picks; they apply to every player in the match
public record MatchSettings
{
    public const int DefaultHealth = 3;
    public const int MinReloadMs = 50;
    public const int MaxReloadMs = 5000;
    public const int DefaultReloadMs = 1000;
    public const int MaxBouncesAllowed = 5;
    public const int MinHealth = 1;
    public const int MaxHealth = 10;
    public const int DefaultBulletSpeed = Bullet.DefaultSpeed;
    // Pixels per tick. Capped so a bubble can't skip past a tank in one step
    public static readonly int[] BulletSpeedChoices = [5, 10, 15, 20, 25, 30, 40];
    public const int DefaultLives = 5;
    public const int MinLives = 1;
    public const int MaxLives = 10;
    public const int MaxRespawnSeconds = 15;
    // A bot match needs at least one human
    public const int MaxBots = 7;
    public static readonly double[] SpeedChoices = [0.5, 0.75, 1, 1.25, 1.5, 2];
    // 0 = no time limit
    public static readonly int[] TimeLimitChoices = [0, 1, 2, 3, 5, 10];
    public GameMode Mode { get; init; } = GameMode.FreeForAll;

    // Milliseconds before a tank can fire again after a shot
    public int ReloadMs { get; init; } = DefaultReloadMs;
    public ProjectileType Projectile { get; init; } = ProjectileType.DumbBubbles;
    // Dumb bubbles only
    public int BulletSpeed { get; init; } = DefaultBulletSpeed;
    public int MaxBounces { get; init; } = 1;
    public int Health { get; init; } = DefaultHealth;
    // Deaths a tank can take; the last death is permanent
    public int Lives { get; init; } = DefaultLives;
    // Wait between dying and coming back (0 = next tick)
    public int RespawnSeconds { get; init; } = 3;
    public double SpeedMultiplier { get; init; } = 1;
    public int TimeLimitMinutes { get; init; } = 0;
    // Computer tanks in the match (With bots: at least 1; Multiplayer: 0 means none)
    public int BotCount { get; init; }
    public BotDifficulty BotDifficulty { get; init; } = BotDifficulty.Medium;
    // Multiplayer only: the last human also has to knock out every bot before winning
    public bool ClearBotsToWin { get; init; }

    // Clients can send anything; snap every value to an allowed choice
    public static MatchSettings Sanitize(MatchSettings incoming) => new()
    {
        ReloadMs = Math.Clamp(incoming.ReloadMs, MinReloadMs, MaxReloadMs),
        Mode = Enum.IsDefined(incoming.Mode) ? incoming.Mode : GameMode.FreeForAll,
        Projectile = Enum.IsDefined(incoming.Projectile) ? incoming.Projectile : ProjectileType.DumbBubbles,
        BulletSpeed = BulletSpeedChoices.MinBy(choice => Math.Abs((long)choice - incoming.BulletSpeed)),
        MaxBounces = Math.Clamp(incoming.MaxBounces, 0, MaxBouncesAllowed),
        Health = Math.Clamp(incoming.Health, MinHealth, MaxHealth),
        Lives = Math.Clamp(incoming.Lives, MinLives, MaxLives),
        RespawnSeconds = Math.Clamp(incoming.RespawnSeconds, 0, MaxRespawnSeconds),
        SpeedMultiplier = double.IsFinite(incoming.SpeedMultiplier)
            ? SpeedChoices.MinBy(choice => Math.Abs(choice - incoming.SpeedMultiplier))
            : 1,
        // long math: int.MinValue would overflow Math.Abs
        TimeLimitMinutes = TimeLimitChoices.MinBy(choice => Math.Abs((long)choice - incoming.TimeLimitMinutes)),
        BotCount = Math.Clamp(incoming.BotCount, 0, MaxBots),
        BotDifficulty = Enum.IsDefined(incoming.BotDifficulty) ? incoming.BotDifficulty : BotDifficulty.Medium,
        ClearBotsToWin = incoming.ClearBotsToWin,
    };

    // Health, lives, the time limit and the bot rules can't change once the match is running
    public MatchSettings WithLockedFrom(MatchSettings current) =>
        this with
        {
            Health = current.Health, Lives = current.Lives, TimeLimitMinutes = current.TimeLimitMinutes,
            BotCount = current.BotCount, ClearBotsToWin = current.ClearBotsToWin,
            Mode = current.Mode
        };

    // Tank speed scales top speed and acceleration together so handling feels the same; turning is unchanged
    public DeveloperGameSettings ScaleMovement(DeveloperGameSettings baseSettings) => baseSettings with
    {
        MaxSpeed = Math.Max(1, (int)Math.Round(baseSettings.MaxSpeed * SpeedMultiplier)),
        ForwardAcceleration = Math.Max(1, (int)Math.Round(baseSettings.ForwardAcceleration * SpeedMultiplier)),
        BrakeAcceleration = (int)Math.Round(baseSettings.BrakeAcceleration * SpeedMultiplier),
    };
}
