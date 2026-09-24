namespace GameLogic;

public record GameMap(string Name, int Width, int Height, IReadOnlyList<Obstacle> Obstacles)
{
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

public record Obstacle(int X, int Y, int Width, int Height)
{
    public bool ContainsPoint(int x, int y) =>
        x >= X &&
        x <= X + Width &&
        y >= Y &&
        y <= Y + Height;

    public bool Intersects(RectangleArea area) =>
        X < area.X + area.Width &&
        X + Width > area.X &&
        Y < area.Y + area.Height &&
        Y + Height > area.Y;
}

public record RectangleArea(int X, int Y, int Width, int Height);

public static class MapCatalog
{
    public static IReadOnlyList<GameMap> FixedMaps { get; } =
    [
        new(
            "Crossfire",
            900,
            700,
            [
                new Obstacle(410, 120, 80, 460),
                new Obstacle(190, 310, 520, 70)
            ]),
        new(
            "Twin Forts",
            900,
            700,
            [
                new Obstacle(170, 130, 140, 210),
                new Obstacle(590, 360, 140, 210),
                new Obstacle(390, 290, 120, 120)
            ]),
        new(
            "Switchbacks",
            900,
            700,
            [
                new Obstacle(150, 120, 560, 60),
                new Obstacle(190, 320, 560, 60),
                new Obstacle(150, 520, 560, 60)
            ]),
        new(
            "Center Wall",
            900,
            700,
            [
                new Obstacle(390, 90, 120, 220),
                new Obstacle(390, 390, 120, 220),
                new Obstacle(130, 300, 160, 90),
                new Obstacle(610, 300, 160, 90)
            ])
    ];

    public static GameMap DefaultMap => FixedMaps[0];

    public static GameMap GetByName(string? mapName)
    {
        return FixedMaps.FirstOrDefault(map => map.Name == mapName) ?? DefaultMap;
    }
}
