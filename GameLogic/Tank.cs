namespace GameLogic;

public record Tank
{
    public const int Size = 60;
    private const int VisualTopOffset = 50;
    private const int HitboxInset = 12;
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
        var newSprite = incomingTank;
        double backwardSpeedModifier = 0.65;

        double radians = Math.PI * incomingTank.Angle / 180.0;
        var deltaX = (int)(incomingTank.Speed * Math.Cos(radians));
        var deltaY = (int)(incomingTank.Speed * Math.Sin(radians));
        var backDeltaX = (int)(incomingTank.Speed * Math.Cos(radians) * backwardSpeedModifier);
        var backDeltaY = (int)(incomingTank.Speed * Math.Sin(radians) * backwardSpeedModifier);

        if (incomingTank.LastDirectionWasBackwards)
        {
            newSprite = incomingTank with
            {
                PositionX = Math.Clamp(incomingTank.PositionX - backDeltaX, 0, map.Width - Size),
                PositionY = Math.Clamp(incomingTank.PositionY - backDeltaY, 0, map.Height - Size)
            };

        }
        else
        {
            newSprite = incomingTank with
            {
                PositionX = Math.Clamp(incomingTank.PositionX + deltaX, 0, map.Width - Size),
                PositionY = Math.Clamp(incomingTank.PositionY + deltaY, 0, map.Height - Size)
            };

        }

        var tankArea = GetCollisionArea(newSprite);
        return map.Blocks(tankArea) ? incomingTank with { Speed = 0 } : newSprite;
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
