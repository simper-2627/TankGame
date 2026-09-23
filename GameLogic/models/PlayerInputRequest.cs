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
}