using GameLogic;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;

// Lets a script in the page act as any player, e.g. for driving two tanks from one browser tab:
//   DotNet.invokeMethodAsync('GameFrontend', 'Input', game, playerId, up, down, left, right, shoot, aimX, aimY)
public static class DebugBridge
{
  public static SignalRService? Service;

  [JSInvokable]
  public static Task Input(string gameName, string playerId, bool up, bool down, bool left, bool right, bool shoot, int aimX, int aimY, bool boost = false)
  {
    var request = new PlayerInputRequest
    {
      GameName = gameName,
      PlayerId = Guid.Parse(playerId),
      Up = up, Down = down, Left = left, Right = right,
      Shoot = shoot,
      Boost = boost,
      AimX = aimX, AimY = aimY,
    };
    return Service?.HubConnection?.SendAsync(Messages.PlayerInput, request) ?? Task.CompletedTask;
  }

  // Adds another tank to a game; its id shows up in the game state
  [JSInvokable]
  public static Task Join(string gameName) =>
    Service?.HubConnection?.SendAsync(Messages.JoinGame, gameName, "", null) ?? Task.CompletedTask;
}
