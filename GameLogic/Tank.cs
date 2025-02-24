namespace GameLogic;

public record Tank
{
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
    public List<Bullet> Bullets { get; init; } = new();

    private const int ForwardMovementSpeedConst = 8;
    private const int BackwardMovementSpeedConst = -6;
    private const int MovementAngleConst = 30;
    private const int BoardSize = 700;
    private const int DefaultSpeedDelta = -6;

    public static Tank ProcessTankMovement(Tank tank)
    {
        var turnedShip = CalculateNewAngleAndSpeed(tank);
        var movedShip = CalculateNewPosition(turnedShip);
        var shootingShip = CalculateShooting(movedShip);
        return movedShip;
    }

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

    private static Tank CalculateNewPosition(Tank incomingTank)
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
                PositionX = Math.Clamp(incomingTank.PositionX - backDeltaX, 0, BoardSize),
                PositionY = Math.Clamp(incomingTank.PositionY - backDeltaY, 0, BoardSize)
            };

        }
        else
        {
            newSprite = incomingTank with
            {
                PositionX = Math.Clamp(incomingTank.PositionX + deltaX, 0, BoardSize),
                PositionY = Math.Clamp(incomingTank.PositionY + deltaY, 0, BoardSize)
            };

        }
        return newSprite;
    }

    private static Tank CalculateShooting(Tank incomingTank)
    {
        if (incomingTank.Shooting)
        {
            var bullet = new Bullet
            {
                PositionX = incomingTank.PositionX,
                PositionY = incomingTank.PositionY,
                Angle = incomingTank.Angle
            };
            incomingTank.Bullets.Add(bullet);
        }
        var updatedBullets = incomingTank.Bullets.Select(b => Bullet.MoveBullet(b)).ToList();
        return incomingTank with
        {
            Bullets = updatedBullets
        };
    }

}