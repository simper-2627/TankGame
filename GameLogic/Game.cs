using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace GameLogic.Game;

public class Game
{
    private readonly IHubContext<LobbyHub> hubContext;
    internal object StateLock { get; } = new();
    // Milliseconds; reload is measured against this, so it isn't limited to the 100 ms tick. Tests swap it for a fake clock
    internal Random SpawnRandom { get; init; } = Random.Shared;
    public Func<long> Clock { get; init; } = () => Environment.TickCount64;

    public GameStatus Status { get; private set; } = GameStatus.Playing;
    // Null while playing, and also when an ended match is a draw
    public Guid? WinnerId { get; private set; }
    // Game loop ticks processed so far (10 per second)
    public int Tick { get; internal set; }
    // Tick when the 2nd player joined; the time limit counts from here
    public int? StartedAtTick { get; private set; }
    public int? TicksLeft => Settings.TimeLimitMinutes == 0 || StartedAtTick is null
        ? null
        : StartedAtTick.Value + Settings.TimeLimitMinutes * 60 * GameLoopRunner.TicksPerSecond - Tick;
    //public event Action? OnUpdate;
    // Connection id -> the player watching on it (null for a connection that isn't playing); decides who gets private state
    public readonly ConcurrentDictionary<string, Guid?> ConnectedClients = new();
    public string? Name { get; init; }
    public string MatchType { get; init; } = GameMatchTypes.Multiplayer;
    public DeveloperGameSettings DeveloperSettings { get; private set; } = new();
    public GameMap Map { get; init; } = MapCatalog.DefaultMap;
    private MatchSettings settings = new();
    public MatchSettings Settings { get => settings; init => settings = MatchSettings.Sanitize(value); }
    // First player to join; only they can change settings during the match
    public Guid? CreatorId { get; private set; }
    public IEnumerable<Tank> Tanks { get; internal set; } = [];
    public IEnumerable<Bullet> Bullets { get; internal set; } = [];
    public IEnumerable<Explosion> Explosions { get; internal set; } = [];
    public CancellationTokenSource CancellationTokenSource { get; set; } = new CancellationTokenSource();
    public GameLoopRunner loopRunner { get; set; }

    public Game(IHubContext<LobbyHub> context)
    {
        loopRunner = new(this);
        hubContext = context;
    }

    public double ServerWorkMs { get; internal set; }
    public double ServerIntervalMs { get; internal set; }
    public double ServerBroadcastMs { get; internal set; }

    // viewerId is whose eyes this is for: values only that player may know (own health and lives, reload, respawn spot)
    // are left out of everyone else's copy. Once the match has ended nothing is hidden any more.
    public GameState GetGameState(bool includeMap = true, Guid? viewerId = null)
    {
        var now = Clock();
        return Snapshot(includeMap, Tanks.Select(t => ToTankState(t, t.Id == viewerId, now)).ToArray());
    }

    private GameState Snapshot(bool includeMap, TankState[] tanks)
    {
        return new()
        {
            Tick = Tick, ServerWorkMs = ServerWorkMs, ServerIntervalMs = ServerIntervalMs, ServerBroadcastMs = ServerBroadcastMs,
            Status = Status,
            Name = Name,
            MatchType = MatchType,
            DeveloperSettings = DeveloperSettings,
            Settings = Settings,
            CreatorId = CreatorId,
            WinnerId = WinnerId,
            SecondsLeft = TicksLeft is int ticksLeft
                ? (Math.Max(0, ticksLeft) + GameLoopRunner.TicksPerSecond - 1) / GameLoopRunner.TicksPerSecond
                : null,
            Map = includeMap ? Map : null,
            Tanks = tanks,
            Explosions = Explosions.Select(e => new ExplosionState()
            {
                Id = e.Id,
                X = e.X,
                Y = e.Y,
                FromX = e.FromX,
                FromY = e.FromY,
                Age = Explosion.Ticks - e.TicksLeft
            }).ToArray(),
            Bullets = Bullets.Select(b => new BulletState()
            {
                Id = b.Id,
                PositionX = b.PositionX,
                PositionY = b.PositionY,
                Angle = b.Angle,
                OwnerId = b.OwnerId
            }).ToArray()
        };
    }

    private TankState ToTankState(Tank t, bool isOwner, long now)
    {
        var revealed = isOwner || Status == GameStatus.Ended;
        return new TankState()
        {
            Id = t.Id,
            Name = t.Name,
            IsBot = t.IsBot,
            InputSequence = t.InputSequence,
            PositionX = t.PositionX,
            PositionY = t.PositionY,
            Angle = t.Angle,
            TurretAngle = t.TurretAngle,
            // A destroyed tank's 0 health is public (it shows as respawning or out); a living tank's isn't
            Health = revealed || t.Health <= 0 ? t.Health : null,
            Eliminated = t.Eliminated,
            Deaths = revealed ? t.Deaths : null,
            RespawnTicksLeft = t.RespawnTicksLeft,
            Flashing = t.HitFlashTicks > 0,
            PendingSpawn = isOwner ? t.PendingSpawn : null,
            ReloadMsLeft = isOwner ? (int)Math.Max(0, t.NextShotAtMs - now) : null,
            HitsLanded = t.HitsLanded,
        };
    }

    public Task SendInitialUpdate(string connectionId, Guid? playerId = null) =>
        hubContext.Clients.Client(connectionId).SendAsync(Messages.GameUpdate, GetGameState(viewerId: playerId));

    public async Task BroadcastUpdate()
    {
        if (ConnectedClients.IsEmpty) return;
        var clients = ConnectedClients.ToArray();
        var now = Clock();
        var tanks = Tanks.ToArray();
        var publicTanks = tanks.Select(t => ToTankState(t, false, now)).ToArray();
        var shared = Snapshot(includeMap: false, publicTanks);

        // Each player gets the shared snapshot with only their own tank swapped for the full version
        var sends = new List<Task>();
        var anonymous = new List<string>();
        foreach (var (connectionId, playerId) in clients)
        {
            var index = playerId is { } id ? Array.FindIndex(tanks, t => t.Id == id) : -1;
            if (index < 0)
            {
                anonymous.Add(connectionId);
                continue;
            }
            var own = (TankState[])publicTanks.Clone();
            own[index] = ToTankState(tanks[index], true, now);
            sends.Add(hubContext.Clients.Client(connectionId).SendAsync(Messages.GameUpdate, shared with { Tanks = own }));
        }
        if (anonymous.Count > 0)
            sends.Add(hubContext.Clients.Clients(anonymous).SendAsync(Messages.GameUpdate, shared));
        await Task.WhenAll(sends);
    }

    // A blank name (quick join, or no name set) gets a generated one that no one else in the game has
    public Guid JoinGame(string? playerName = null)
    {
        lock (StateLock)
        {
        if (Status == GameStatus.Ended)
            throw new InvalidOperationException($"cannot join game, it has ended: {Name}");

        if (Tanks.Count() >= Map.MaxPlayers)
            throw new InvalidOperationException($"cannot join game, lobby is full: {Name}");
        var spawnPoint = SpawnSelector.Choose(Map, Tanks, SpawnRandom, DeveloperSettings);
        var newTank = new Tank
        {
            Name = string.IsNullOrWhiteSpace(playerName)
                ? PlayerNames.Generate(Tanks.Select(t => t.Name), Random.Shared)
                : playerName.Trim(),
            PositionX = spawnPoint?.X ?? 0,
            PositionY = spawnPoint?.Y ?? 0,
            Angle = spawnPoint?.Angle ?? 0,
            TurretAngle = spawnPoint?.Angle ?? 0,
            Health = spawnPoint is null ? 0 : Settings.Health
        };
        Tanks = Tanks.Append(newTank);
        CreatorId ??= newTank.Id;
        if (Tanks.Count() == 2)
            StartedAtTick = Tick;
        return newTank.Id;
        }
    }

    // Returns true when an instant shot just landed, so the caller can push the explosion to clients right away
    public bool ReceiveUserInput(PlayerInputRequest request)
    {
        lock (StateLock)
        {
        if (Status == GameStatus.Ended)
            return false;

        Tank? instantShooter = null;
        Tanks = Tanks.Select(t =>
        {
            // Eliminated (or respawning) players keep watching but can't drive or shoot
            if (t.Id == request.PlayerId && !t.Eliminated && !t.Respawning)
            {

                var updatedTank = t with
                {
                    InputSequence = request.InputSequence,
                    MovingUp = request.Up,
                    MovingLeft = request.Left,
                    MovingRight = request.Right,
                    Shooting = request.Shoot,
                    MovingDown = request.Down,
                    AimX = request.AimX ?? t.AimX,
                    AimY = request.AimY ?? t.AimY,
                };
                updatedTank = Tank.AimTurret(updatedTank, DeveloperSettings);

                // Fire once per press, and only when reloaded; a press during reload is dropped, not queued
                var now = Clock();
                if (updatedTank.Shooting && !t.Shooting && now >= t.NextShotAtMs)
                {
                    if (Settings.Projectile == ProjectileType.Realistic)
                        instantShooter = updatedTank;
                    else
                        Bullets = Bullets.Append(Tank.FireBullet(updatedTank, DeveloperSettings, Settings.MaxBounces, Settings.BulletSpeed));
                    updatedTank = updatedTank with { NextShotAtMs = now + Settings.ReloadMs };
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

        if (instantShooter is not null)
            FireInstantShot(instantShooter);
        return instantShooter is not null;
        }
    }

    // Realistic projectile: lands the moment it's fired. Damage only counts once a 2nd player has joined
    private void FireInstantShot(Tank shooter)
    {
        var tanks = Tanks.ToList();
        var shot = Combat.TraceShot(shooter, tanks, Map, DeveloperSettings);
        if (shot.HitIndex is int hitIndex && StartedAtTick is not null)
        {
            Combat.ApplyHit(tanks, hitIndex, shooter.Id, Settings);
            Tanks = tanks;
        }
        var (centerX, centerY) = Tank.GetCenter(shooter, DeveloperSettings);
        var radians = Math.PI * shooter.TurretAngle / 180.0;
        var muzzleX = centerX + (int)Math.Round(Tank.BarrelLength * Math.Cos(radians));
        var muzzleY = centerY + (int)Math.Round(Tank.BarrelLength * Math.Sin(radians));
        Explosions = Explosions.Append(Explosion.At(shot.X, shot.Y, muzzleX, muzzleY)).ToArray();
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
            TurnDegrees = Math.Clamp(settings.TurnDegrees, 1, 180),
            BackwardSpeedMultiplier = Math.Clamp(settings.BackwardSpeedMultiplier, 0.1, 1.5)
        };
    }

    // Only the creator can change settings; health, lives and time limit stay as the match started (except in developer simulation)
    public void UpdateMatchSettings(Guid playerId, MatchSettings incoming)
    {
        lock (StateLock)
        {
            if (playerId != CreatorId || Status == GameStatus.Ended)
                return;
            var sanitized = MatchSettings.Sanitize(incoming);
            // Developer simulation is for experimenting, so nothing stays locked there
            if (MatchType != GameMatchTypes.DeveloperSimulation)
            {
                settings = sanitized.WithLockedFrom(settings);
                return;
            }

            var healthChanged = sanitized.Health != settings.Health;
            settings = sanitized;
            // A new health value applies to everyone still playing, so the change is visible straight away
            if (healthChanged)
                Tanks = Tanks.Select(t => t.Eliminated || t.Respawning ? t : t with { Health = sanitized.Health }).ToArray();
        }
    }

    internal void ApplyResult(MatchResult result)
    {
        if (!result.Ended)
            return;
        Status = GameStatus.Ended;
        WinnerId = result.WinnerId;
    }

}

public enum GameStatus
{
    NotStarted,
    Playing,
    Ended,
}
