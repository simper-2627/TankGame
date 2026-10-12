using GameLogic;
using GameLogic.Profiles;

namespace GameTest;

public class EarningsTrackerTests
{
    // Expected amounts come from the rates, so retuning the economy doesn't break these tests
    private const int TicksPerSecond = 10;
    private static readonly CurrencyRates Rates = CurrencyRates.Default;
    private static readonly int TicksPerCash = Rates.SecondsPerCash * TicksPerSecond;

    private static EarningsTracker NewTracker() => new(Rates, TicksPerSecond);

    private static void TickFor(EarningsTracker tracker, Tank tank, int ticks)
    {
        for (var i = 0; i < ticks; i++)
            tracker.Tick([tank]);
    }

    [Fact]
    public void TimePaysOneCashPerInterval()
    {
        var tracker = NewTracker();
        var tank = new Tank();
        tracker.Register(tank.Id);

        TickFor(tracker, tank, TicksPerCash);

        Assert.Equal(new Earnings(Cash: 1, Seconds: Rates.SecondsPerCash, Hits: 0, Kills: 0), tracker.Flush(tank));
    }

    [Fact]
    public void PartOfAnIntervalCarriesOverToTheNextPayout()
    {
        var tracker = NewTracker();
        var tank = new Tank();
        tracker.Register(tank.Id);

        // One second short of a payout, then the last second
        TickFor(tracker, tank, TicksPerCash - TicksPerSecond);
        Assert.Equal(new Earnings(0, Rates.SecondsPerCash - 1, 0, 0), tracker.Flush(tank));
        TickFor(tracker, tank, TicksPerSecond);
        Assert.Equal(new Earnings(1, 1, 0, 0), tracker.Flush(tank));
    }

    [Fact]
    public void HitsAndKillsPay()
    {
        var tracker = NewTracker();
        var tank = new Tank();
        tracker.Register(tank.Id);

        var earned = tracker.Flush(tank with { HitsLanded = 3, Kills = 1 });

        Assert.Equal(new Earnings(Cash: 3 * Rates.CashPerHit + Rates.CashPerKill, Seconds: 0, Hits: 3, Kills: 1), earned);
    }

    [Fact]
    public void AFlushOnlyPaysWhatIsNew()
    {
        var tracker = NewTracker();
        var tank = new Tank();
        tracker.Register(tank.Id);
        var threeHits = tank with { HitsLanded = 3 };

        tracker.Flush(threeHits);
        Assert.True(tracker.Flush(threeHits).IsEmpty);

        Assert.Equal(new Earnings(Rates.CashPerHit, 0, 1, 0), tracker.Flush(tank with { HitsLanded = 4 }));
    }

    [Fact]
    public void AnEliminatedTankStopsEarningTime()
    {
        var tracker = NewTracker();
        var tank = new Tank { Eliminated = true };
        tracker.Register(tank.Id);

        TickFor(tracker, tank, TicksPerCash);

        Assert.True(tracker.Flush(tank).IsEmpty);
    }

    [Fact]
    public void AStoppedTankStopsEarningTime()
    {
        var tracker = NewTracker();
        var tank = new Tank();
        tracker.Register(tank.Id);
        TickFor(tracker, tank, TicksPerCash);

        tracker.Stop(tank.Id);
        TickFor(tracker, tank, TicksPerCash);

        Assert.Equal(new Earnings(1, Rates.SecondsPerCash, 0, 0), tracker.Flush(tank));
    }

    [Fact]
    public void ATankThatWasNeverRegisteredEarnsNothing()
    {
        var tracker = NewTracker();
        var tank = new Tank { HitsLanded = 5, Kills = 2 };

        TickFor(tracker, tank, TicksPerCash);

        Assert.True(tracker.Flush(tank).IsEmpty);
    }

    [Fact]
    public void TotalShowsEverythingEarnedSoFarWithoutPayingIt()
    {
        var tracker = NewTracker();
        var tank = new Tank();
        tracker.Register(tank.Id);
        TickFor(tracker, tank, TicksPerCash);
        var played = tank with { HitsLanded = 2, Kills = 1 };

        var expected = new Earnings(
            Cash: 1 + 2 * Rates.CashPerHit + Rates.CashPerKill, Seconds: Rates.SecondsPerCash, Hits: 2, Kills: 1);
        Assert.Equal(expected, tracker.Total(played));
        Assert.Equal(expected, tracker.Total(played));
        Assert.Equal(expected, tracker.Flush(played));
    }

    [Fact]
    public void TotalKeepsCountingAfterAPayout()
    {
        var tracker = NewTracker();
        var tank = new Tank();
        tracker.Register(tank.Id);
        TickFor(tracker, tank, TicksPerCash);
        tracker.Flush(tank with { HitsLanded = 1 });

        var total = tracker.Total(tank with { HitsLanded = 2 });

        Assert.Equal(1 + 2 * Rates.CashPerHit, total!.Cash);
    }

    [Fact]
    public void TotalIsNullForATankThatWasNeverRegistered()
    {
        Assert.Null(NewTracker().Total(new Tank { HitsLanded = 3 }));
    }
}
