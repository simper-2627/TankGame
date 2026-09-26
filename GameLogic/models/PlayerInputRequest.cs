namespace GameLogic;
public record PlayerInputRequest
{
  public required string GameName { get; init; }
  public required Guid PlayerId { get; init; }
  public required bool Up { get; init; }
  public required bool Left { get; init; }
  public required bool Right { get; init; }
  public required bool Down { get; init; }
  public required bool Shoot { get; init; }
  // Mouse position on the board; null until the mouse has moved over it
  public int? AimX { get; init; }
  public int? AimY { get; init; }
}