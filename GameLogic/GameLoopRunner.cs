namespace GameLogic.Game;

public class GameLoopRunner
{
    private readonly Game game;
    private object loopLock { get; } = new object();
    private object fileLock { get; } = new object();
    private bool loopIsRunning { get; set; } = false;
    public static double TickIntervalScalar = 10;

    private int tickcounter = 0;

    IEnumerable<Tank> lastTankState;

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
        game.Tanks = game.Tanks.Select(Tank.ProcessTankMovement).ToArray();


        _ = Task.Run(() =>
        {
            if (game.Tanks != lastTankState)
            {


                // put the append function here.
                // append a line with the array

                string ticklist = "{";
                ticklist += $"{tickcounter}, ";

                foreach (var thing in game.Tanks)
                {
                    ticklist += thing.ToString();
                    ticklist += ",";
                }

                ticklist.Remove(ticklist.Length - 4, 4); // remove the last ', '
                ticklist += "},\n";

                //Console.WriteLine(ticklist);

                // now I just need to output it to a game location.
                _ = Task.Run(() =>
                {

                    if (!File.Exists($"{game.Name}.txt"))
                    {
                         File.Create($"{game.Name}.txt").Close();

                        
                    }

                    lock (fileLock)
                    {
                        File.AppendAllText($"{game.Name}.txt", ticklist);
                    }
                });

                lastTankState = game.Tanks;
            }
        });

        await game.BroadcastUpdate();
    }
}