namespace GameLogic.Game;

public class GameLoopRunner
{
  private readonly Game game;
  private object loopLock { get; } = new object();
  private bool loopIsRunning { get; set; } = false;
  public static double TickIntervalScalar = 10;

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
        var interval = (int)(10 * TickIntervalScalar);
        // Console.WriteLine($"sleeping {interval}");
        
        Thread.Sleep(interval);
      }
      loopIsRunning = false;
      
    });
  }
  public async Task ProcessGameTick()
  {
    Console.WriteLine("processing game tick");

    game.Tanks = game.Tanks.Select(Tank.ProcessTankMovement).ToArray();
    await game.BroadcastUpdate();
  }
}