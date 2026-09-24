using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace GameLogic.Game;

public class Game
{
    private readonly IHubContext<LobbyHub> hubContext;
    internal object StateLock { get; } = new();

    public GameStatus Status => GameStatus.Playing;
    //public event Action? OnUpdate;
    public readonly ConcurrentDictionary<string, byte> ConnectedClients = new();
    public string? Name { get; init; }
    public string MatchType { get; init; } = GameMatchTypes.Multiplayer;
    public DeveloperGameSettings DeveloperSettings { get; private set; } = new();
    public GameMap Map { get; init; } = MapCatalog.DefaultMap;
    public IEnumerable<Tank> Tanks { get; internal set; } = [];
    public IEnumerable<Bullet> Bullets { get; internal set; } = [];
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
            MatchType = MatchType,
            DeveloperSettings = DeveloperSettings,
            Map = Map,
            Tanks = Tanks.Select(t => new TankState()
            {
                Id = t.Id,
                PositionX = t.PositionX,
                PositionY = t.PositionY,
                Angle = t.Angle,

            }).ToArray(),
            Bullets = Bullets.Select(b => new BulletState()
            {
                Id = b.Id,
                PositionX = b.PositionX,
                PositionY = b.PositionY,
                Angle = b.Angle
            }).ToArray()
        };
    }

    public async Task BroadcastUpdate()
    {
        await hubContext.Clients.Clients(ConnectedClients.Keys.ToArray()).SendAsync(Messages.GameUpdate, GetGameState());
    }

    public Guid JoinGame()
    {
        lock (StateLock)
        {
        var spawnPoint = Map.SpawnPoints.ElementAt(Tanks.Count() % Map.SpawnPoints.Count);
        var newTank = new Tank
        {
            PositionX = spawnPoint.X,
            PositionY = spawnPoint.Y,
            Angle = spawnPoint.Angle
        };
        Tanks = Tanks.Append(newTank);
        return newTank.Id;
        }
    }

    public void ReceiveUserInput(PlayerInputRequest request)
    {
        lock (StateLock)
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
                    Bullets = Bullets.Append(bullet);
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

    public void UpdateDeveloperSettings(DeveloperGameSettings settings)
    {
        if (MatchType != GameMatchTypes.DeveloperSimulation)
        {
            return;
        }

        DeveloperSettings = settings with
        {
            HitboxInset = Math.Clamp(settings.HitboxInset, 0, Tank.Size / 2 - 1),
            VisualTopOffset = Math.Clamp(settings.VisualTopOffset, 0, Tank.Size),
            CollisionStepPixels = Math.Clamp(settings.CollisionStepPixels, 1, 12),
            ForwardAcceleration = Math.Clamp(settings.ForwardAcceleration, 1, 30),
            BrakeAcceleration = Math.Clamp(settings.BrakeAcceleration, -30, 0),
            MaxSpeed = Math.Clamp(settings.MaxSpeed, 1, 160),
            TurnDegrees = Math.Clamp(settings.TurnDegrees, 1, 90),
            BackwardSpeedMultiplier = Math.Clamp(settings.BackwardSpeedMultiplier, 0.1, 1.5)
        };
    }

}

public enum GameStatus
{
    NotStarted,
    Playing,
    Ended,
}
