using GameLogic.Game;

namespace GameLogic
{
    internal class ReplaySaver
    {
        IEnumerable<Tank> lastTankState;
        private object fileLock { get; } = new object();


        public void SaveTick(IEnumerable<Tank> tanks, int tickcounter, string gameName)
        {

            _ = Task.Run(() =>
            {
                if (tanks != lastTankState)
                {


                    // put the append function here.
                    // append a line with the array

                    string ticklist = "{";
                    ticklist += $"{tickcounter}, ";

                    foreach (var thing in tanks)
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

                        if (!File.Exists($"{gameName}.txt"))
                        {
                            File.Create($"{gameName}.txt").Close();
                        }

                        lock (fileLock)
                        {
                            File.AppendAllText($"{gameName}.txt", ticklist);
                        }
                    });

                    lastTankState = tanks;
                }
            });
        }
    }
}
