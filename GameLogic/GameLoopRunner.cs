using System.Diagnostics;
namespace GameLogic.Game;

public class GameLoopRunner
{
    private readonly Game game;
    private object loopLock { get; } = new object();
    private bool loopIsRunning { get; set; } = false;
    public static double TickIntervalScalar = 10;
    // Matches the default 100 ms tick interval
    public const int TicksPerSecond = 10;

    private long lastTickAt;
    public GameLoopRunner(Game game)
    {
        this.game = game;
    }


    public void RunGameLoop()
    {
        lock (loopLock)
        {
            if (loopIsRunning) return;
            loopIsRunning = true;
        }
        _ = Task.Run(async () =>
        {
            try
            {
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(10 * TickIntervalScalar));
                do { await ProcessGameTick(); }
                while (await timer.WaitForNextTickAsync(game.CancellationTokenSource.Token));
            }
            catch (OperationCanceledException) when (game.CancellationTokenSource.IsCancellationRequested) { }
            finally { lock (loopLock) loopIsRunning = false; }
        });
    }
    public async Task ProcessGameTick()
    {
        var tickAt = Stopwatch.GetTimestamp();
        if (lastTickAt != 0) game.ServerIntervalMs = Stopwatch.GetElapsedTime(lastTickAt, tickAt).TotalMilliseconds;
        lastTickAt = tickAt;
        // Input and simulation both replace state; keep either update from overwriting the other.
        lock (game.StateLock)
        {
        game.Explosions = game.Explosions
            .Select(explosion => explosion with { TicksLeft = explosion.TicksLeft - 1 })
            .Where(explosion => explosion.TicksLeft > 0)
            .ToArray();
        // An ended match is frozen; updates keep going out so everyone sees the result
        if (game.Status != GameStatus.Ended)
        {
            game.Tick++;
            game.RunBots();
            var movement = game.Settings.ScaleMovement(game.DeveloperSettings);
            game.Tanks = game.Tanks
                .Select(tank => Tank.ProcessTankMovement(tank, game.Map, movement))
                .Select(tank => tank.HitFlashTicks > 0 ? tank with { HitFlashTicks = tank.HitFlashTicks - 1 } : tank)
                .Select(tank => tank.ShieldTicksLeft > 0 ? tank with { ShieldTicksLeft = tank.ShieldTicksLeft - 1 } : tank)
                .ToArray();
            game.Bullets = game.Bullets
                .Select(bullet => Bullet.MoveBullet(bullet, game.Map))
                .Where(bullet => bullet is not null)
                .Cast<Bullet>()
                .ToArray();
            // No damage until the 2nd player joins: a creator practising alone can't eliminate themselves
            if (game.StartedAtTick is not null)
            {
                var (tanks, bullets) = Combat.ResolveHits(game.Tanks, game.Bullets, game.DeveloperSettings, game.Settings);
                game.Tanks = tanks;
                game.Bullets = bullets;
            }
            game.Tanks = Combat.TickRespawns(game.Tanks, game.Map, game.Settings, Random.Shared, game.DeveloperSettings);
            game.ApplyResult(Combat.DecideResult(game.Tanks.ToArray(), game.TicksLeft,
                singlePlayer: game.MatchType == GameMatchTypes.Bots, clearBotsToWin: game.Settings.ClearBotsToWin));
        }
        }

        game.ServerWorkMs = Stopwatch.GetElapsedTime(tickAt).TotalMilliseconds;
        var broadcastAt = Stopwatch.GetTimestamp();
        await game.BroadcastUpdate();
        game.ServerBroadcastMs = Stopwatch.GetElapsedTime(broadcastAt).TotalMilliseconds;
    }
}
