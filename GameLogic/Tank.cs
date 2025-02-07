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

  private const int MovementSpeedConst = 8;
  private const int MovementAngleConst = 30;
  private const int BoardSize = 700;

  public static Tank ProcessTankMovement(Tank tank)
  {
    var turnedShip = CalculateNewAngleAndSpeed(tank);
    var movedShip = CalculateNewPosition(turnedShip);
    return movedShip;
  }

  private static Tank CalculateNewAngleAndSpeed(Tank tank)
  {
    var nextAngle = tank.Angle;
    if (tank.MovingLeft)
    {
      nextAngle -= MovementAngleConst;
    }
    else if (tank.MovingRight)
    {
      nextAngle += MovementAngleConst;
    }

    var speedDelta = tank.MovingForward ? MovementSpeedConst : (-1 * MovementSpeedConst);
    var newSpeed = Math.Clamp(
      tank.Speed + speedDelta,
      0,
      10 * MovementSpeedConst
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

    double radians = Math.PI * incomingTank.Angle / 180.0;
    var deltaX = (int)(incomingTank.Speed * Math.Cos(radians));
    var deltaY = (int)(incomingTank.Speed * Math.Sin(radians));
    var newSprite = incomingTank with
    {
      PositionX = Math.Clamp(incomingTank.PositionX + deltaX, 0, BoardSize),
      PositionY = Math.Clamp(incomingTank.PositionY + deltaY, 0, BoardSize)
    };
    return newSprite;
  }

}