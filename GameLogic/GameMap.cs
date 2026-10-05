namespace GameLogic;

public record GameMap(
    string Name,
    int Width,
    int Height,
    IReadOnlyList<Obstacle> Obstacles,
    IReadOnlyList<MapSpawnPoint> SpawnPoints)
{
    public MapMode Mode { get; init; } = MapMode.BigMap;
    public int MaxPlayers { get; init; } = 8;
    public int ViewWidth { get; init; } = 900;
    public int ViewHeight { get; init; } = 700;
    public IReadOnlyList<Obstacle> GroundPatches { get; init; } = [];
    public string GroundColor { get; init; } = "#d8d2b5";

    public bool Contains(RectangleArea area) =>
        area.X >= 0 &&
        area.Y >= 0 &&
        area.X + area.Width <= Width &&
        area.Y + area.Height <= Height;

    public bool Blocks(RectangleArea area) =>
        !Contains(area) || Obstacles.Any(obstacle => obstacle.Intersects(area));

    public bool BlocksPoint(int x, int y) =>
        x < 0 ||
        y < 0 ||
        x > Width ||
        y > Height ||
        Obstacles.Any(obstacle => obstacle.ContainsPoint(x, y));
}

public record MapSpawnPoint(int X, int Y, int Angle);

public record RectangleArea(int X, int Y, int Width, int Height)
{
    public bool Intersects(RectangleArea other) =>
        X < other.X + other.Width &&
        X + Width > other.X &&
        Y < other.Y + other.Height &&
        Y + Height > other.Y;
}

public interface IMapSource
{
    IReadOnlyList<GameMap> Maps { get; }
    GameMap DefaultMap { get; }
    GameMap GetByName(string? mapName);
}

// Kept as a compatibility entry point for callers using the original source name.
public sealed class FixedMapSource : JsonMapSource { }

public static class MapCatalog
{
    private static readonly IMapSource Source = new JsonMapSource();
    public static IReadOnlyList<GameMap> FixedMaps => Source.Maps;
    public static GameMap DefaultMap => Source.DefaultMap;
    public static GameMap GetByName(string? mapName) => Source.GetByName(mapName);
}
