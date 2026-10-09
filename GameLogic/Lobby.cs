using System.Collections.Concurrent;
using GameLogic;
using GameLogic.Game;
using GameLogic.Profiles;
using Microsoft.AspNetCore.SignalR;

public class Lobby
{
  public List<Game> Games { get; set; } = new();
  public IProfileStore Profiles { get; }
  private readonly IHubContext<LobbyHub> context;
  private readonly IMapSource mapSource;

  //public event Action? OnLobbyUpdate;
  public Lobby(IHubContext<LobbyHub> context, IMapSource? mapSource = null, IProfileStore? profileStore = null)
  {
    this.context = context;
    this.mapSource = mapSource ?? new FixedMapSource();
    Profiles = profileStore ?? new InMemoryProfileStore();
  }

  // A known profile decides the player's name and earns currency; an unknown or missing one (for example after a
  // server restart) joins anonymously with the name that was typed, if any
  public async Task<(string? Name, Guid? ProfileId)> ResolvePlayerAsync(string? playerName, Guid? profileId)
  {
    if (profileId is { } id && await Profiles.GetAsync(id) is { } profile)
      return (profile.DisplayName, id);
    return (playerName, null);
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
      Settings = chosen,
      // The developer sandbox lets one player add targets and set health to 1, so it must not pay out
      ProfileStore = type == GameMatchTypes.DeveloperSimulation ? null : Profiles
    };

    Games.Add(newGame);
    return newGame;
  }
}
