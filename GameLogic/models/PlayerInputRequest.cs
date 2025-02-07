namespace GameLogic;
public record PlayerInputRequest
{
  public required string GameName { get; init; }
  public required Guid PlayerId { get; init; }
  public required bool Forward { get; init; }
  public required bool Left { get; init; }
  public required bool Right { get; init; }
}