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

    // The player is back (reconnected or rejoined): time counts again
    public void Resume(Guid tankId)
    {
        if (entries.TryGetValue(tankId, out var entry))
            entry.Active = true;
    }

    // Everything this tank has earned so far (paid or not), for showing the player. Changes nothing.
    // Null for a tank that never earns (bots, anonymous players)
    public Earnings? Total(Tank tank) =>
        entries.TryGetValue(tank.Id, out var entry) ? TotalFor(tank, entry) : null;

    private Earnings TotalFor(Tank tank, Entry entry)
    {
        // Paid out as whole cash; the remainder of an interval stays in Ticks for next time
        var seconds = entry.Ticks / ticksPerSecond;
        var cash = seconds / rates.SecondsPerCash
            + (long)tank.HitsLanded * rates.CashPerHit
            + (long)tank.Kills * rates.CashPerKill;
        return new Earnings(cash, seconds, tank.HitsLanded, tank.Kills);
    }

    public Earnings Flush(Tank tank)
    {
        if (!entries.TryGetValue(tank.Id, out var entry))
            return new Earnings(0, 0, 0, 0);

        var total = TotalFor(tank, entry);
        var earned = new Earnings(
            Cash: total.Cash - entry.PaidCash,
            Seconds: total.Seconds - entry.PaidSeconds,
            Hits: total.Hits - entry.PaidHits,
            Kills: total.Kills - entry.PaidKills);

        entry.PaidCash = total.Cash;
        entry.PaidSeconds = total.Seconds;
        entry.PaidHits = total.Hits;
        entry.PaidKills = total.Kills;
        return earned;
    }
}
