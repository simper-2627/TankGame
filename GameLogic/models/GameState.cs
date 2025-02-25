using GameLogic.Game;

namespace GameLogic;

public record GameState
{
    public GameStatus Status { get; init; }
    public string? Name { get; init; }
    public IEnumerable<TankState>? Tanks { get; init; }
}

public record TankState
{
    public Guid Id { get; init; }
    public int PositionX { get; init; }
    public int PositionY { get; init; }
    public int Angle { get; init; }
    public BulletState? Bullet { get; init; } = new();
}

public record BulletState
{
    public int PositionX { get; init; }
    public int PositionY { get; init; }
    public int Angle { get; init; }
}