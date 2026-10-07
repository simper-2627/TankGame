namespace GameLogic;

public static class SpawnSelector
{
    public const int Clearance = Tank.Size;

    public static MapSpawnPoint? Choose(GameMap map, IEnumerable<Tank> tanks, Random rng, DeveloperGameSettings? settings = null)
    {
        settings ??= new DeveloperGameSettings();
        var living = Living(tanks);
        var free = map.SpawnPoints.Where(p => IsFree(map, living, p, settings)).ToArray();
        return free.Length == 0 ? null : free[rng.Next(free.Length)];
    }

    // Still open: not inside a wall and a full tank width from every living tank
    public static bool IsFree(GameMap map, IEnumerable<Tank> tanks, MapSpawnPoint point, DeveloperGameSettings? settings = null)
    {
        settings ??= new DeveloperGameSettings();
        return IsFree(map, Living(tanks), point, settings);
    }

    private static Tank[] Living(IEnumerable<Tank> tanks) => tanks.Where(t => !t.Eliminated && !t.Respawning).ToArray();

    private static bool IsFree(GameMap map, Tank[] living, MapSpawnPoint p, DeveloperGameSettings settings)
    {
        var box = new RectangleArea(p.X, p.Y - settings.VisualTopOffset, Tank.Size, Tank.Size);
        var clearance = new RectangleArea(p.X - Clearance, p.Y - settings.VisualTopOffset - Clearance,
            Tank.Size + 2 * Clearance, Tank.Size + 2 * Clearance);
        return !map.Blocks(box) && !living.Any(t => clearance.Intersects(
            new RectangleArea(t.PositionX, t.PositionY - settings.VisualTopOffset, Tank.Size, Tank.Size)));
    }
}
