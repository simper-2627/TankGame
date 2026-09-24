using GameLogic.Game;

namespace GameLogic;

public record GameState
{
    public GameStatus Status { get; init; }
    public string? Name { get; init; }
    public string MatchType { get; init; } = GameMatchTypes.Multiplayer;
    public DeveloperGameSettings DeveloperSettings { get; init; } = new();
    public GameMap? Map { get; init; }
    public IEnumerable<TankState>? Tanks { get; init; }
    public IEnumerable<BulletState>? Bullets { get; init; }
}

public static class GameMatchTypes
{
    public const string Multiplayer = "Multiplayer";
    public const string Bots = "With bots";
    public const string DeveloperSimulation = "Developer simulation";
}

public record DeveloperGameSettings
{
    public int HitboxInset { get; init; } = 6;
    public int VisualTopOffset { get; init; } = 50;
    public int CollisionStepPixels { get; init; } = 1;
    public int ForwardAcceleration { get; init; } = 8;
    public int BrakeAcceleration { get; init; } = -6;
    public int MaxSpeed { get; init; } = 80;
    public int TurnDegrees { get; init; } = 30;
    public double BackwardSpeedMultiplier { get; init; } = 0.65;
}

public record TankState
{
    public Guid Id { get; init; }
    public int PositionX { get; init; }
    public int PositionY { get; init; }
    public int Angle { get; init; }
}

public record BulletState
{
    public int PositionX { get; init; }
    public int PositionY { get; init; }
    public int Angle { get; init; }
}
