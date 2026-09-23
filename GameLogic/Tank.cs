namespace GameLogic;

public record Tank
{
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
    //public Bullet Bullet { get; set; } = new();

    private const int MovementSpeedConst = 8;
    private const int BoardSize = 700;

    public static Tank ProcessTankMovement(Tank tank)
    {
        var turnedShip = CalculateNewAngleAndSpeed(tank);
        var movedShip = CalculateNewPosition(turnedShip);
        //CalculateShooting(movedShip);
        return movedShip;
    }

    private static Tank CalculateNewAngleAndSpeed(Tank tank)
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
            Speed = MovementSpeedConst,
        };
    }

    private static Tank CalculateNewPosition(Tank incomingTank)
    {
        double radians = Math.PI * incomingTank.Angle / 180.0;
        var deltaX = (int)(incomingTank.Speed * Math.Cos(radians));
        var deltaY = (int)(incomingTank.Speed * Math.Sin(radians));

        return incomingTank with
        {
            PositionX = Math.Clamp(incomingTank.PositionX + deltaX, 0, BoardSize),
            PositionY = Math.Clamp(incomingTank.PositionY + deltaY, 0, BoardSize)
        };
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