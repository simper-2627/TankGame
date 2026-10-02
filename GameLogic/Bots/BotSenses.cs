namespace GameLogic.Bots;

// Questions a bot asks about the world. Everything works from the same TankState and BulletState a human's screen gets
public static class BotSenses
{
    // Smaller than any wall or tank, so a ray can't step over one
    public const int LineOfSightStep = 8;
    public const double ThreatDistance = 40;
    public const int ThreatLookAheadTicks = 6;

    // Middle of the drawn tank, which is where the turret pivots
    public static (double X, double Y) Center(TankState tank, DeveloperGameSettings settings) =>
        (tank.PositionX + Tank.Size / 2.0, tank.PositionY - settings.VisualTopOffset + Tank.Size / 2.0);

    public static double Distance((double X, double Y) a, (double X, double Y) b)
    {
        var (dx, dy) = (b.X - a.X, b.Y - a.Y);
        return Math.Sqrt(dx * dx + dy * dy);
    }

    // A straight line between the two points that no wall crosses
    public static bool HasLineOfSight(GameMap map, (double X, double Y) from, (double X, double Y) to)
    {
        var steps = (int)Math.Ceiling(Distance(from, to) / LineOfSightStep);
        for (var i = 1; i < steps; i++)
        {
            var t = i / (double)steps;
            var x = (int)Math.Round(from.X + (to.X - from.X) * t);
            var y = (int)Math.Round(from.Y + (to.Y - from.Y) * t);
            if (map.BlocksPoint(x, y))
                return false;
        }
        return true;
    }

    // If this bullet will pass close enough to the tank to hit it within the next few ticks, the direction to dodge in
    public static (double X, double Y)? Threat(BulletState bullet, (double X, double Y) tankCenter, double bulletSpeed)
    {
        var radians = Math.PI * bullet.Angle / 180.0;
        var (vx, vy) = (bulletSpeed * Math.Cos(radians), bulletSpeed * Math.Sin(radians));
        var (bx, by) = (bullet.PositionX + Bullet.BulletSize / 2.0, bullet.PositionY + Bullet.BulletSize / 2.0);
        var (rx, ry) = (tankCenter.X - bx, tankCenter.Y - by);
        var speedSquared = vx * vx + vy * vy;
        var along = rx * vx + ry * vy;
        // Not moving, or flying away from the tank
        if (speedSquared == 0 || along <= 0)
            return null;

        var ticks = Math.Min(along / speedSquared, ThreatLookAheadTicks);
        var closest = (X: bx + vx * ticks, Y: by + vy * ticks);
        if (Distance(closest, tankCenter) > ThreatDistance)
            return null;

        // Sidestep across the bullet's line, toward whichever side the tank is already on
        var (px, py) = (-vy, vx);
        var side = rx * px + ry * py >= 0 ? 1 : -1;
        return (px * side, py * side);
    }
}

// Picks which human a bot is after: the closest one, but it only switches to another human once that one has been
// clearly closer for a while, so it doesn't flip between two humans at similar distances
public sealed class TargetTracker
{
    public const int SwitchTicks = 10;
    // The challenger must be at least 25% closer
    public const double SwitchRatio = 0.75;

    private Guid? targetId;
    private Guid? challengerId;
    private int challengerTicks;

    public TankState? Choose(IReadOnlyList<TankState> humans, Func<TankState, double> distanceTo)
    {
        if (humans.Count == 0)
        {
            Reset();
            return null;
        }

        var nearest = humans.MinBy(distanceTo)!;
        var current = humans.FirstOrDefault(human => human.Id == targetId);
        if (current is null)
        {
            targetId = nearest.Id;
            challengerId = null;
            return nearest;
        }

        if (nearest.Id != current.Id && distanceTo(nearest) <= distanceTo(current) * SwitchRatio)
        {
            if (challengerId != nearest.Id)
            {
                challengerId = nearest.Id;
                challengerTicks = 0;
            }
            if (++challengerTicks >= SwitchTicks)
            {
                targetId = nearest.Id;
                challengerId = null;
                return nearest;
            }
        }
        else
        {
            challengerId = null;
        }
        return current;
    }

    public void Reset()
    {
        targetId = null;
        challengerId = null;
        challengerTicks = 0;
    }
}
