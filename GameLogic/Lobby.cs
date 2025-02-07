using System.Collections.Concurrent;
using GameLogic.Game;
using Microsoft.AspNetCore.SignalR;

public class Lobby
{
  public List<Game> Games { get; set; } = new();
  private readonly IHubContext<LobbyHub> context;

  public event Action OnLobbyUpdate;
  public Lobby(IHubContext<LobbyHub> context)
  {
    this.context = context;
  }

  public Game CreateGame(string name)
  {
    var newGame = new Game(context)
    {
      Name = name
    };

    Games.Add(newGame);
    return newGame;
  }
}