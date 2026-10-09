using System.Text.Json;
using GameLogic;
using GameLogic.Game;
using GameLogic.Profiles;
using Microsoft.AspNetCore.SignalR;


public class LobbyHub : Hub
{
  private readonly Lobby lobby;
  public LobbyHub(Lobby lobby)
  {
    this.lobby = lobby;
  }
  public long PerformancePing() => Environment.TickCount64;

  // The id comes from the player's browser. A missing or unknown one (first visit, or the server restarted) gets a new profile;
  // the client stores whatever id comes back. The profile is returned to the caller only
  public async Task<Profile> GetOrCreateProfile(Guid? profileId)
  {
    if (profileId is { } id && await lobby.Profiles.GetAsync(id) is { } existing)
      return existing;
    return await lobby.Profiles.CreateAsync();
  }

  public async Task<Profile> RenameProfile(Guid profileId, string name)
  {
    if (!ProfileName.TryNormalize(name, out var clean))
      throw new HubException($"Names must be 1-{ProfileName.MaxLength} characters.");
    return await lobby.Profiles.RenameAsync(profileId, clean)
      ?? throw new HubException("Profile not found. Reload the page to start a new one.");
  }

  public async Task SendMessage(string user, string message)
  {
    await Clients.All.SendAsync("ReceiveMessage", user, message);
  }

  // SignalR doesn't fill optional parameters, so clients must send every argument
  public async Task CreateGame(string name, string? mapName = null, string? matchType = null, MatchSettings? settings = null, string? playerName = null, Guid? profileId = null)
  {
    var nameTaken = lobby.Games.FirstOrDefault(g => g.Name == name) != null;
    if(nameTaken)
    {
      throw new Exception($"cannot create game, name already taken: {name}");
    }

    var game = lobby.CreateGame(name, mapName, matchType, settings);
    Console.WriteLine($"created game: {name}");

    var (resolvedName, resolvedProfile) = await lobby.ResolvePlayerAsync(playerName, profileId);
    var playerId = game.JoinGame(resolvedName, resolvedProfile);

    await Clients.Client(Context.ConnectionId).SendAsync(Messages.CreatedGame, game.Name, playerId);
    game.loopRunner.RunGameLoop();

    var games = lobby.Games.Select(g => g.GetGameState()).ToArray();
    await Clients.All.SendAsync(Messages.GameList, games);
  }

  public async Task JoinGame(string gameName, string? playerName = null, Guid? profileId = null)
  {
    var game = lobby.Games.FirstOrDefault(g => g.Name == gameName)
      ?? throw new HubException($"Battle '{gameName}' is no longer available. Return to the lobby to create or join a battle.");
    var (resolvedName, resolvedProfile) = await lobby.ResolvePlayerAsync(playerName, profileId);
    var playerId = game.JoinGame(resolvedName, resolvedProfile);
    await SubscribeToGame(gameName, playerId);
    await Clients.Client(Context.ConnectionId).SendAsync(Messages.JoinedGame, game.Name, playerId);
    await Clients.All.SendAsync(Messages.GameList, lobby.Games.Select(g => g.GetGameState()).ToArray());
  }

  public async Task GetGames()
  {
    var games = lobby.Games.Select(g => g.GetGameState()).ToArray();
    Console.WriteLine("got request for games");

    await Clients.Client(Context.ConnectionId).SendAsync(Messages.GameList, games);
  }

  // playerId lets the game send this connection its own private state (SignalR doesn't fill optional parameters, so clients send both)
  public async Task SubscribeToGame(string gameName, Guid? playerId = null)
  {
    Console.WriteLine("subscribing to game");

    var game = lobby.Games.FirstOrDefault(g => g.Name == gameName)
      ?? throw new HubException($"Battle '{gameName}' is no longer available. Return to the lobby to create or join a battle.");

    // Deliver immutable map data once, before enrolling this connection in live updates.
    await game.SendInitialUpdate(Context.ConnectionId, playerId);
    game.ConnectedClients[Context.ConnectionId] = playerId;
    if (playerId is { } id)
      game.ResumeEarning(id);

  }

  public async Task UnsubscribeFromGame(string gameName)
  {
    var game = lobby.Games.FirstOrDefault(g => g.Name == gameName);
    if (game is not null && game.ConnectedClients.TryRemove(Context.ConnectionId, out var playerId) && playerId is { } id)
      await game.PlayerLeftAsync(id);
  }

  public async Task PlayerInput(PlayerInputRequest request)
  {

    var game = lobby.Games.First(g => g.Name == request.GameName);
    // An instant shot shouldn't wait for the next 100 ms tick to show its explosion
    if (game.ReceiveUserInput(request))
      await game.BroadcastUpdate();
  }

  public async Task UpdateDeveloperSettings(string gameName, DeveloperGameSettings settings)
  {
    var game = lobby.Games.FirstOrDefault(g => g.Name == gameName)
      ?? throw new HubException($"Battle '{gameName}' is no longer available. Return to the lobby to create or join a battle.");
    game.UpdateDeveloperSettings(settings);
    await game.BroadcastUpdate();
  }

  public async Task UpdateMatchSettings(string gameName, Guid playerId, MatchSettings settings)
  {
    var game = lobby.Games.FirstOrDefault(g => g.Name == gameName)
      ?? throw new HubException($"Battle '{gameName}' is no longer available. Return to the lobby to create or join a battle.");
    game.UpdateMatchSettings(playerId, settings);
    await game.BroadcastUpdate();
  }

  public async Task AddBot(string gameName)
  {
    var game = lobby.Games.FirstOrDefault(g => g.Name == gameName)
      ?? throw new HubException($"Battle '{gameName}' is no longer available. Return to the lobby to create or join a battle.");
    if (game.AddBot() is null)
      throw new HubException("cannot add a bot to this battle");
    await game.BroadcastUpdate();
  }

  public override async Task OnDisconnectedAsync(Exception? exception)
  {
    string? connectionId = Context.ConnectionId;

    foreach (var game in lobby.Games)
    {
      if (game.ConnectedClients.TryRemove(connectionId, out var playerId))
      {
        Console.WriteLine($"Removed connection: {connectionId} from game {game.Name}");
        if (playerId is { } id)
          await game.PlayerLeftAsync(id);
      }
    }
    await base.OnDisconnectedAsync(exception);
  }

}
