using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace GameLogic.Game;

public class Game
{
    private readonly IHubContext<LobbyHub> hubContext;

    public GameStatus Status => GameStatus.Playing;
    //public event Action? OnUpdate;
    public readonly ConcurrentBag<string> ConnectedClients = new();
    public string? Name { get; init; }
    public IEnumerable<Tank> Tanks { get; internal set; } = [];
    public CancellationTokenSource CancellationTokenSource { get; set; } = new CancellationTokenSource();
    public GameLoopRunner loopRunner { get; set; }

    public Game(IHubContext<LobbyHub> context)
    {
        loopRunner = new(this);
        hubContext = context;
    }

    public GameState GetGameState()
    {
        return new()
        {
            Status = Status,
            Name = Name,
            Tanks = Tanks.Select(t => new TankState()
            {
                Id = t.Id,
                PositionX = t.PositionX,
                PositionY = t.PositionY,
                Angle = t.Angle,

            }).ToArray()
        };
    }

    public async Task BroadcastUpdate()
    {
        await hubContext.Clients.Clients(ConnectedClients.ToArray()).SendAsync(Messages.GameUpdate, GetGameState());
    }

    public Guid JoinGame()
    {
        var newTank = new Tank();
        Tanks = Tanks.Append(newTank);
        return newTank.Id;
    }

    public void ReceiveUserInput(PlayerInputRequest request)
    {
        Tanks = Tanks.Select(t =>
        {
            if (t.Id == request.PlayerId)
            {

                var updatedTank = t with
                {
                    MovingForward = request.Forward,
                    MovingLeft = request.Left,
                    MovingRight = request.Right,
                    Shooting = request.Shoot,
                    MovingBackward = request.Backward,
                    LastDirectionWasBackwards = request.LastDirectionBackwards
                };

                if (updatedTank.Shooting)
                {
                    var bullet = new Bullet
                    {
                        PositionX = updatedTank.PositionX,
                        PositionY = updatedTank.PositionY,
                        Angle = updatedTank.Angle
                    };
                }

                //if (updatedTank.Bullet != null)
                //{
                //    updatedTank = updatedTank with
                //    {
                //        Bullet = Bullet.MoveBullet(updatedTank)
                //    };
                //}
                return updatedTank;
            }
            return t;
        })
        .ToArray();

    }
}

public enum GameStatus
{
    NotStarted,
    Playing,
    Ended,
}