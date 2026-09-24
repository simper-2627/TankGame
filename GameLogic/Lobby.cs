using System.Collections.Concurrent;
using GameLogic;
using GameLogic.Game;
using Microsoft.AspNetCore.SignalR;

public class Lobby
{
  public List<Game> Games { get; set; } = new();
  private readonly IHubContext<LobbyHub> context;

  //public event Action? OnLobbyUpdate;
  public Lobby(IHubContext<LobbyHub> context)
  {
    this.context = context;
  }

  public Game CreateGame(string name, string? mapName = null, string? matchType = null)
  {
    var newGame = new Game(context)
    {
      Name = name,
      MatchType = matchType == GameMatchTypes.DeveloperSimulation
        ? GameMatchTypes.DeveloperSimulation
        : GameMatchTypes.Multiplayer,
      Map = MapCatalog.GetByName(mapName)
    };

    Games.Add(newGame);
    return newGame;
  }
}
