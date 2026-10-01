using GameLogic.Game;

namespace GameLogic;

public record GameState
{
    public int Tick { get; init; }
    public double ServerWorkMs { get; init; }
    public double ServerIntervalMs { get; init; }
    public double ServerBroadcastMs { get; init; }
    public GameStatus Status { get; init; }
    public string? Name { get; init; }
    public string MatchType { get; init; } = GameMatchTypes.Multiplayer;
    public DeveloperGameSettings DeveloperSettings { get; init; } = new();
    public MatchSettings Settings { get; init; } = new();
    public Guid? CreatorId { get; init; }
    public Guid? WinnerId { get; init; }
    // The bots won (every human is out, or time ran out in single player); WinnerId is null then
    public bool BotsWon { get; init; }
    // Null when there's no time limit or it hasn't started
    public int? SecondsLeft { get; init; }
    public GameMap? Map { get; init; }
    public IEnumerable<TankState>? Tanks { get; init; }
    public IEnumerable<BulletState>? Bullets { get; init; }
    public IEnumerable<ExplosionState>? Explosions { get; init; }
}

public static class GameMatchTypes
{
    public const string Multiplayer = "Multiplayer";
    public const string Bots = "With bots";
    public const string DeveloperSimulation = "Developer simulation";
}

public record DeveloperGameSettings
{
    public bool SlideAlongWalls { get; init; }
    public int HitboxInset { get; init; } = 8;
    public int VisualTopOffset { get; init; } = 26;
    public int CollisionStepPixels { get; init; } = 1;
    public int ForwardAcceleration { get; init; } = 8;
    public int BrakeAcceleration { get; init; } = -16;
    public int MaxSpeed { get; init; } = 8;
    public int TurnDegrees { get; init; } = 180;
    public double BackwardSpeedMultiplier { get; init; } = 0.65;
}

public record TankState
{
    public long InputSequence { get; init; }
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public bool IsBot { get; init; }
    public int PositionX { get; init; }
    public int PositionY { get; init; }
    public int Angle { get; init; }
    public int TurretAngle { get; init; }
    // Private while the tank is alive: only its owner (and everyone, once the match ends) gets the number
    public int? Health { get; init; }
    public bool Eliminated { get; init; }
    // Private like Health
    public int? Deaths { get; init; }
    public int RespawnTicksLeft { get; init; }
    // Just got hit. Public, so everyone can see the damage without learning how much health is left
    public bool Flashing { get; init; }
    // Private: only filled in for the tank's own viewer, null for everyone else
    public MapSpawnPoint? PendingSpawn { get; init; }
    // Private: ms until the tank can fire again (0 = ready); null for everyone but the owner
    public int? ReloadMsLeft { get; init; }
    public int HitsLanded { get; init; }
    // Destroyed with lives left, waiting to come back
    public bool Respawning => Health <= 0 && !Eliminated;
}

public record BulletState
{
    public Guid Id { get; init; }
    public int PositionX { get; init; }
    public int PositionY { get; init; }
    public int Angle { get; init; }
    // Tank that fired it, so the client can color the shot like its shooter
    public Guid OwnerId { get; init; }
}
