namespace GameLogic.Game;

public class GameLoopRunner
{
    private readonly Game game;
    private object loopLock { get; } = new object();
    private bool loopIsRunning { get; set; } = false;
    public static double TickIntervalScalar = 10;

    private int tickcounter = 0;

    private ReplaySaver? saver;
    public GameLoopRunner(Game game)
    {
        this.game = game;
    }


    public void RunGameLoop()
    {
        Task.Run(async () =>
        {
            game.CancellationTokenSource.Token.ThrowIfCancellationRequested();
            lock (loopLock)
            {
                if (loopIsRunning)
                {
                    Console.WriteLine("Another thread is already running the loop.");
                    return;
                }

                loopIsRunning = true;
            }

            while (!game.CancellationTokenSource.Token.IsCancellationRequested)
            {
                await ProcessGameTick();
                tickcounter++;
                var interval = (int)(10 * TickIntervalScalar);
                // Console.WriteLine($"sleeping {interval}");

                Thread.Sleep(interval);
            }
            loopIsRunning = false;

        });
    }
    public async Task ProcessGameTick()
    {
        Console.WriteLine($"processing game tick {tickcounter}");

        //var copy = game.Tanks.ToArray();
        //foreach (var tank in copy) {
        //    Console.WriteLine(tank);
        //}
        //foreach (var tank in game.Tanks) {
        //    Console.WriteLine(tank);
        //}
        //Console.WriteLine();
        game.Tanks = game.Tanks.Select(tank => Tank.ProcessTankMovement(tank, game.Map, game.DeveloperSettings)).ToArray();
        game.Bullets = game.Bullets
            .Select(bullet => Bullet.MoveBullet(bullet, game.Map))
            .Where(bullet => bullet is not null)
            .Cast<Bullet>()
            .ToArray();

        saver?.SaveTick(game.Tanks, tickcounter, game.Name ?? string.Empty);

        await game.BroadcastUpdate();
    }
}
