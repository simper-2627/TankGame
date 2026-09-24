namespace GameLogic;

public record Tank
{
    public const int Size = 60;
    public Guid Id { get; } = Guid.NewGuid();
    public int PositionY { get; init; } = 50;
    public int PositionX { get; init; } = 50;
    public int Angle { get; init; } = -45;
    public int Speed { get; init; } = 0;
    public bool MovingForward { get; init; }
    public bool MovingLeft { get; init; }
    public bool MovingRight { get; init; }
    public bool Shooting { get; init; }
    public bool MovingBackward { get; init; }
    public bool LastDirectionWasBackwards { get; init; }
    //public Bullet Bullet { get; set; } = new();

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
        return movedShip;
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
        int speedDelta;
        var nextAngle = tank.Angle;
        if (tank.MovingLeft)
        {
            nextAngle -= settings.TurnDegrees;
        }
        else if (tank.MovingRight)
        {
            nextAngle += settings.TurnDegrees;
        }
        if (tank.MovingForward)
        {
            speedDelta = settings.ForwardAcceleration;
        }

        else if (tank.MovingBackward)
        {
            speedDelta = settings.ForwardAcceleration;
        }
        else
        {
            speedDelta = settings.BrakeAcceleration;
        }

        var newSpeed = Math.Clamp(
        tank.Speed + speedDelta,
        0,
        settings.MaxSpeed
      );

        var turnedShip = tank with
        {
            Speed = newSpeed,
            Angle = nextAngle,
        };
        return turnedShip;
    }

    private static Tank CalculateNewPosition(Tank incomingTank, GameMap map, DeveloperGameSettings settings)
    {
        double radians = Math.PI * incomingTank.Angle / 180.0;
        var deltaX = (int)(incomingTank.Speed * Math.Cos(radians));
        var deltaY = (int)(incomingTank.Speed * Math.Sin(radians));
        var backDeltaX = (int)(incomingTank.Speed * Math.Cos(radians) * settings.BackwardSpeedMultiplier);
        var backDeltaY = (int)(incomingTank.Speed * Math.Sin(radians) * settings.BackwardSpeedMultiplier);

        if (incomingTank.LastDirectionWasBackwards)
        {
            return MoveUntilBlocked(incomingTank, -backDeltaX, -backDeltaY, map, settings);
        }

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
