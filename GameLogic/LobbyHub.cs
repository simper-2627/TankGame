using System.Text.Json;
using GameLogic;
using GameLogic.Game;
using Microsoft.AspNetCore.SignalR;


public class LobbyHub : Hub
{
  private readonly Lobby lobby;
  public LobbyHub(Lobby lobby)
  {
    this.lobby = lobby;
  }
  public async Task SendMessage(string user, string message)
  {
    await Clients.All.SendAsync("ReceiveMessage", user, message);
  }

  public async Task CreateGame(string name)
  {
    var nameTaken = lobby.Games.FirstOrDefault(g => g.Name == name) != null;
    if(nameTaken)
    {
      throw new Exception($"cannot create game, name already taken: {name}");
    }

    var game = lobby.CreateGame(name);
    Console.WriteLine($"created game: {name}");

    var playerId = game.JoinGame();

    await Clients.Client(Context.ConnectionId).SendAsync(Messages.CreatedGame, game.Name, playerId);
    game.loopRunner.RunGameLoop();

    var games = lobby.Games.Select(g => g.GetGameState()).ToArray();
    await Clients.All.SendAsync(Messages.GameList, games);
  }

  public async Task JoinGame(string gameName)
  {
    var game = lobby.Games.First(g => g.Name == gameName);
    var playerId = game.JoinGame();
    SubscribeToGame(gameName);
    await Clients.Client(Context.ConnectionId).SendAsync(Messages.JoinedGame, game.Name, playerId);
  }

  public async Task GetGames()
  {
    var games = lobby.Games.Select(g => g.GetGameState()).ToArray();
    Console.WriteLine("got request for games");

    await Clients.Client(Context.ConnectionId).SendAsync(Messages.GameList, games);
  }

  public void SubscribeToGame(string gameName)
  {
    Console.WriteLine("subscribing to game");

    var game = lobby.Games.First(g => g.Name == gameName);

    game.ConnectedClients.Add(Context.ConnectionId);

  }

  public void PlayerInput(PlayerInputRequest request)
  {
    Console.WriteLine("got player input");
    Console.WriteLine(request);
    var game = lobby.Games.First(g => g.Name == request.GameName);
    game.ReceiveUserInput(request);
  }

  public override async Task OnDisconnectedAsync(Exception? exception)
  {
    string? connectionId = Context.ConnectionId;

    foreach (var game in lobby.Games)
    {
      if (game.ConnectedClients.TryTake(out connectionId))
      {
        Console.WriteLine($"Removed connection: {connectionId} from game {game.Name}");
      }
    }
    await base.OnDisconnectedAsync(exception);
  }

}