using System.Collections.Concurrent;
using GameLogic;
using GameLogic.Game;
using Microsoft.AspNetCore.SignalR;

public class Lobby
{
  public List<Game> Games { get; set; } = new();
  private readonly IHubContext<LobbyHub> context;
  private readonly IMapSource mapSource;

  //public event Action? OnLobbyUpdate;
  public Lobby(IHubContext<LobbyHub> context, IMapSource? mapSource = null)
  {
    this.context = context;
    this.mapSource = mapSource ?? new FixedMapSource();
  }

  public Game CreateGame(string name, string? mapName = null, string? matchType = null, MatchSettings? settings = null)
  {
    var type = matchType switch
    {
      GameMatchTypes.DeveloperSimulation => GameMatchTypes.DeveloperSimulation,
      GameMatchTypes.Bots => GameMatchTypes.Bots,
      _ => GameMatchTypes.Multiplayer
    };
    var map = mapSource.GetByName(mapName);

    var chosen = MatchSettings.Sanitize(settings ?? new MatchSettings());

    var newGame = new Game(context)
    {
      Name = name,
      MatchType = type,
      Map = map,
      Settings = chosen
    };

    Games.Add(newGame);
    return newGame;
  }
}
