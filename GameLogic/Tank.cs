namespace GameLogic;

public record Tank
{
    public const int Size = 60;
    public Guid Id { get; } = Guid.NewGuid();
    public int PositionY { get; init; } = 50;
    public int PositionX { get; init; } = 50;
    public int Angle { get; init; } = -45;
    public int Speed { get; init; } = 0;
    public bool MovingUp { get; init; }
    public bool MovingLeft { get; init; }
    public bool MovingRight { get; init; }
    public bool Shooting { get; init; }
    public bool MovingDown { get; init; }
    // Point the turret aims at (the player's mouse), in board coordinates
    public int? AimX { get; init; }
    public int? AimY { get; init; }
    public int TurretAngle { get; init; } = -45;
    //public Bullet Bullet { get; set; } = new();

    private const int MovementSpeedConst = 8;

    public static Tank ProcessTankMovement(Tank tank)
    {
        return ProcessTankMovement(tank, MapCatalog.DefaultMap, new DeveloperGameSettings());
    }

    public static Tank ProcessTankMovement(Tank tank, GameMap map)
    {
        return ProcessTankMovement(tank, map, new DeveloperGameSettings());
    }

    public static Tank ProcessTankMovement(Tank tank, GameMap map, DeveloperGameSettings settings)
    {
        var turnedShip = CalculateNewAngleAndSpeed(tank, settings);
        var movedShip = CalculateNewPosition(turnedShip, map, settings);
        //CalculateShooting(movedShip);
        return AimTurret(movedShip, settings);
    }

    // Center of the drawn tank, which the turret rotates around
    public static (int X, int Y) GetCenter(Tank tank, DeveloperGameSettings settings) =>
        (tank.PositionX + Size / 2, tank.PositionY - settings.VisualTopOffset + Size / 2);

    // Re-aim every tick so the turret stays on the cursor while the tank drives
    public static Tank AimTurret(Tank tank, DeveloperGameSettings settings)
    {
        if (tank.AimX is null || tank.AimY is null)
            return tank;

        var (centerX, centerY) = GetCenter(tank, settings);
        var deltaX = tank.AimX.Value - centerX;
        var deltaY = tank.AimY.Value - centerY;
        if (deltaX == 0 && deltaY == 0)
            return tank;

        var newAngle = (int)Math.Round(Math.Atan2(deltaY, deltaX) * 180.0 / Math.PI);
        return tank with { TurretAngle = newAngle };
    }

    public static RectangleArea GetCollisionArea(Tank tank) =>
        GetCollisionArea(tank, new DeveloperGameSettings());

    public static RectangleArea GetCollisionArea(Tank tank, DeveloperGameSettings settings)
    {
        var hitboxInset = Math.Clamp(settings.HitboxInset, 0, Size / 2 - 1);
        var hitboxSize = Size - (hitboxInset * 2);
        return new(
            tank.PositionX + hitboxInset,
            tank.PositionY - settings.VisualTopOffset + hitboxInset,
            hitboxSize,
            hitboxSize);
    }

    private static Tank CalculateNewAngleAndSpeed(Tank tank, DeveloperGameSettings settings)
    {
        var netX = (tank.MovingRight ? 1 : 0) - (tank.MovingLeft ? 1 : 0);
        var netY = (tank.MovingDown ? 1 : 0) - (tank.MovingUp ? 1 : 0);

        if (netX == 0 && netY == 0)
        {
            return tank with { Speed = 0 };
        }

        var newAngle = (int)Math.Round(Math.Atan2(netY, netX) * 180.0 / Math.PI);
        return tank with
        {
            Angle = newAngle,
            Speed = Math.Min(MovementSpeedConst, settings.MaxSpeed),
        };
    }

    private static Tank CalculateNewPosition(Tank incomingTank, GameMap map, DeveloperGameSettings settings)
    {
        double radians = Math.PI * incomingTank.Angle / 180.0;
        var deltaX = (int)(incomingTank.Speed * Math.Cos(radians));
        var deltaY = (int)(incomingTank.Speed * Math.Sin(radians));

        return MoveUntilBlocked(incomingTank, deltaX, deltaY, map, settings);
    }

    private static Tank MoveUntilBlocked(Tank tank, int deltaX, int deltaY, GameMap map, DeveloperGameSettings settings)
    {
        var targetX = Math.Clamp(tank.PositionX + deltaX, 0, map.Width - Size);
        var targetY = Math.Clamp(tank.PositionY + deltaY, 0, map.Height - Size);
        var totalX = targetX - tank.PositionX;
        var totalY = targetY - tank.PositionY;
        // Sliding uses pixel steps so an axis fallback cannot skip a thin obstacle.
        var stepPixels = settings.SlideAlongWalls ? 1 : Math.Max(1, settings.CollisionStepPixels);
        var steps = (int)Math.Ceiling(Math.Max(Math.Abs(totalX), Math.Abs(totalY)) / (double)stepPixels);

        if (steps == 0)
        {
            return tank;
        }

        var lastValidTank = tank;
        for (var step = 1; step <= steps; step++)
        {
            var stepX = (int)Math.Round(totalX * step / (double)steps)
                - (int)Math.Round(totalX * (step - 1) / (double)steps);
            var stepY = (int)Math.Round(totalY * step / (double)steps)
                - (int)Math.Round(totalY * (step - 1) / (double)steps);
            var nextTank = lastValidTank with
            {
                PositionX = lastValidTank.PositionX + stepX,
                PositionY = lastValidTank.PositionY + stepY
            };

            if (map.Blocks(GetCollisionArea(nextTank, settings)))
            {
                if (settings.SlideAlongWalls)
                {
                    var alongX = lastValidTank with { PositionX = nextTank.PositionX };
                    var alongY = lastValidTank with { PositionY = nextTank.PositionY };
                    var canSlideX = stepX != 0 && !map.Blocks(GetCollisionArea(alongX, settings));
                    var canSlideY = stepY != 0 && !map.Blocks(GetCollisionArea(alongY, settings));
                    // At an exact corner neither face is preferred: don't steer the tank sideways.
                    if (canSlideX && canSlideY)
                        return lastValidTank with { Speed = 0 };
                    if (canSlideX)
                    {
                        lastValidTank = alongX;
                        continue;
                    }
                    if (canSlideY)
                    {
                        lastValidTank = alongY;
                        continue;
                    }
                    // A shallow diagonal may have no tangential pixel in this step.
                    continue;
                }
                return lastValidTank with { Speed = 0 };
            }

            lastValidTank = nextTank;
        }

        return lastValidTank.PositionX == tank.PositionX && lastValidTank.PositionY == tank.PositionY
            ? lastValidTank with { Speed = 0 }
            : lastValidTank;
    }

    public static RectangleArea GetVisualArea(Tank tank) =>
        new(
            tank.PositionX,
            tank.PositionY - new DeveloperGameSettings().VisualTopOffset,
            Size,
            Size);

    //private static Bullet CalculateShooting(Tank incomingTank)
    //{
    //    Bullet bullet = new();
    //    if (incomingTank.Shooting)
    //    {
    //        bullet = new Bullet
    //        {
    //            PositionX = incomingTank.PositionX,
    //            PositionY = incomingTank.PositionY,
    //            Angle = incomingTank.Angle
    //        };
            
    //    }
    //    var updatedBullet = Bullet.MoveBullet(bullet);

    //    return updatedBullet;
    //}
}
