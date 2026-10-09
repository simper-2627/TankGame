namespace GameLogic.Profiles;

// Turns a tank's running counters into payouts. It remembers how much it has already paid for each tank, so a
// flush only ever returns what is new (flushing twice, or at disconnect and again at match end, never pays twice).
// Not thread-safe: Game calls it while holding its state lock
public class EarningsTracker(CurrencyRates rates, int ticksPerSecond)
{
    private sealed class Entry
    {
        public long Ticks;
        public bool Active = true;
        public long PaidSeconds;
        public long PaidCash;
        public int PaidHits;
        public int PaidKills;
    }

    private readonly Dictionary<Guid, Entry> entries = new();

    // Only registered tanks earn; bots and anonymous players are never registered
    public void Register(Guid tankId) => entries.TryAdd(tankId, new Entry());

    // One game tick passed: every registered tank still in the match earns time
    public void Tick(IEnumerable<Tank> tanks)
    {
        foreach (var tank in tanks)
            if (entries.TryGetValue(tank.Id, out var entry) && entry.Active && !tank.Eliminated)
                entry.Ticks++;
    }

    // The player left: no more time, but hits and kills already made can still be flushed
    public void Stop(Guid tankId)
    {
        if (entries.TryGetValue(tankId, out var entry))
            entry.Active = false;
    }

    public Earnings Flush(Tank tank)
    {
        if (!entries.TryGetValue(tank.Id, out var entry))
            return new Earnings(0, 0, 0, 0);

        // Totals are paid out as whole cash; the remainder of an interval stays in Ticks for next time
        var seconds = entry.Ticks / ticksPerSecond;
        var totalCash = seconds / rates.SecondsPerCash
            + (long)tank.HitsLanded * rates.CashPerHit
            + (long)tank.Kills * rates.CashPerKill;

        var earned = new Earnings(
            Cash: totalCash - entry.PaidCash,
            Seconds: seconds - entry.PaidSeconds,
            Hits: tank.HitsLanded - entry.PaidHits,
            Kills: tank.Kills - entry.PaidKills);

        entry.PaidCash = totalCash;
        entry.PaidSeconds = seconds;
        entry.PaidHits = tank.HitsLanded;
        entry.PaidKills = tank.Kills;
        return earned;
    }
}
