namespace GameLogic;

public record GameMap(
    string Name,
    int Width,
    int Height,
    IReadOnlyList<Obstacle> Obstacles,
    IReadOnlyList<MapSpawnPoint> SpawnPoints)
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

public record MapSpawnPoint(int X, int Y, int Angle);

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
            ],
            [
                new MapSpawnPoint(60, 60, 0),
                new MapSpawnPoint(780, 580, 180),
                new MapSpawnPoint(780, 60, 135),
                new MapSpawnPoint(60, 580, -45)
            ]),
        new(
            "Twin Forts",
            900,
            700,
            [
                new Obstacle(170, 130, 140, 210),
                new Obstacle(590, 360, 140, 210),
                new Obstacle(390, 290, 120, 120)
            ],
            [
                new MapSpawnPoint(70, 70, 0),
                new MapSpawnPoint(770, 570, 180),
                new MapSpawnPoint(760, 80, 135),
                new MapSpawnPoint(80, 560, -45)
            ]),
        new(
            "Switchbacks",
            900,
            700,
            [
                new Obstacle(150, 120, 560, 60),
                new Obstacle(190, 320, 560, 60),
                new Obstacle(150, 520, 560, 60)
            ],
            [
                new MapSpawnPoint(60, 60, 0),
                new MapSpawnPoint(780, 600, 180),
                new MapSpawnPoint(60, 250, 0),
                new MapSpawnPoint(780, 430, 180)
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
            ],
            [
                new MapSpawnPoint(70, 80, 0),
                new MapSpawnPoint(770, 560, 180),
                new MapSpawnPoint(760, 80, 135),
                new MapSpawnPoint(70, 560, -45)
            ])
    ];

    public static GameMap DefaultMap => FixedMaps[0];

    public static GameMap GetByName(string? mapName)
    {
        return FixedMaps.FirstOrDefault(map => map.Name == mapName) ?? DefaultMap;
    }
}
