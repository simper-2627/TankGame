namespace GameLogic;

public readonly record struct MapCamera(double Scale, double X, double Y)
{
    public static TankState? FollowTarget(IEnumerable<TankState> tanks, Guid playerId, Guid? previousTarget)
    {
        var living = tanks.Where(t => !t.Eliminated && !t.Respawning).ToArray();
        return living.FirstOrDefault(t => t.Id == playerId)
            ?? living.FirstOrDefault(t => t.Id == previousTarget)
            ?? living.FirstOrDefault();
    }

    public static MapCamera For(GameMap map, double centerX, double centerY,
        double? viewportWidth = null, double? viewportHeight = null)
    {
        var width = viewportWidth ?? map.ViewWidth;
        var height = viewportHeight ?? map.ViewHeight;
        // Fill the viewport without stretching terrain. Fog matches default Big Map's
        // 1800 x 1400 viewing area, then follows the tank across its larger world.
        var scale = map.Mode == MapMode.Foggish
            ? Math.Max(width / 1800, height / 1400)
            : Math.Max(width / map.Width, height / map.Height);
        // Even unusually small maps must cover the entire viewport.
        scale = Math.Max(scale, Math.Max(width / map.Width, height / map.Height));
        var visibleWidth = width / scale;
        var visibleHeight = height / scale;
        return new(scale, Math.Clamp(centerX - visibleWidth / 2, 0, Math.Max(0, map.Width - visibleWidth)),
            Math.Clamp(centerY - visibleHeight / 2, 0, Math.Max(0, map.Height - visibleHeight)));
    }

    public (int X, int Y) ToWorld(double screenX, double screenY) =>
        ((int)Math.Round(screenX / Scale + X), (int)Math.Round(screenY / Scale + Y));
}
