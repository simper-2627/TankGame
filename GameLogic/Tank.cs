namespace GameLogic;

public record Tank
{
    public const int Size = 60;
    private const int VisualTopOffset = 50;
    private const int HitboxInset = 6;
    private const int HitboxSize = Size - (HitboxInset * 2);
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

    private const int ForwardMovementSpeedConst = 8;
    private const int BackwardMovementSpeedConst = -6;
    private const int MovementAngleConst = 30;
    private const int DefaultSpeedDelta = -6;

    public static Tank ProcessTankMovement(Tank tank)
    {
        return ProcessTankMovement(tank, MapCatalog.DefaultMap);
    }

    public static Tank ProcessTankMovement(Tank tank, GameMap map)
    {
        var turnedShip = CalculateNewAngleAndSpeed(tank);
        var movedShip = CalculateNewPosition(turnedShip, map);
        //CalculateShooting(movedShip);
        return movedShip;
    }

    public static RectangleArea GetCollisionArea(Tank tank) =>
        new(
            tank.PositionX + HitboxInset,
            tank.PositionY - VisualTopOffset + HitboxInset,
            HitboxSize,
            HitboxSize);

    private static Tank CalculateNewAngleAndSpeed(Tank tank)
    {
        int speedDelta;
        var nextAngle = tank.Angle;
        if (tank.MovingLeft)
        {
            nextAngle -= MovementAngleConst;
        }
        else if (tank.MovingRight)
        {
            nextAngle += MovementAngleConst;
        }
        if (tank.MovingForward)
        {
            speedDelta = tank.MovingForward ? ForwardMovementSpeedConst : (-1 * ForwardMovementSpeedConst);
        }

        else if (tank.MovingBackward)
        {
            speedDelta = tank.MovingBackward ? ForwardMovementSpeedConst : (1 * ForwardMovementSpeedConst);
        }
        else
        {
            speedDelta = BackwardMovementSpeedConst;
        }

        var newSpeed = Math.Clamp(
        tank.Speed + speedDelta,
        0,
        10 * ForwardMovementSpeedConst
      );

        var turnedShip = tank with
        {
            Speed = newSpeed,
            Angle = nextAngle,
        };
        return turnedShip;
    }

    private static Tank CalculateNewPosition(Tank incomingTank, GameMap map)
    {
        double backwardSpeedModifier = 0.65;

        double radians = Math.PI * incomingTank.Angle / 180.0;
        var deltaX = (int)(incomingTank.Speed * Math.Cos(radians));
        var deltaY = (int)(incomingTank.Speed * Math.Sin(radians));
        var backDeltaX = (int)(incomingTank.Speed * Math.Cos(radians) * backwardSpeedModifier);
        var backDeltaY = (int)(incomingTank.Speed * Math.Sin(radians) * backwardSpeedModifier);

        if (incomingTank.LastDirectionWasBackwards)
        {
            return MoveUntilBlocked(incomingTank, -backDeltaX, -backDeltaY, map);
        }

        return MoveUntilBlocked(incomingTank, deltaX, deltaY, map);
    }

    private static Tank MoveUntilBlocked(Tank tank, int deltaX, int deltaY, GameMap map)
    {
        var targetX = Math.Clamp(tank.PositionX + deltaX, 0, map.Width - Size);
        var targetY = Math.Clamp(tank.PositionY + deltaY, 0, map.Height - Size);
        var totalX = targetX - tank.PositionX;
        var totalY = targetY - tank.PositionY;
        var steps = Math.Max(Math.Abs(totalX), Math.Abs(totalY));

        if (steps == 0)
        {
            return tank;
        }

        var lastValidTank = tank;
        for (var step = 1; step <= steps; step++)
        {
            var nextTank = tank with
            {
                PositionX = tank.PositionX + (int)Math.Round(totalX * step / (double)steps),
                PositionY = tank.PositionY + (int)Math.Round(totalY * step / (double)steps)
            };

            if (map.Blocks(GetCollisionArea(nextTank)))
            {
                return lastValidTank with { Speed = 0 };
            }

            lastValidTank = nextTank;
        }

        return lastValidTank;
    }

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
