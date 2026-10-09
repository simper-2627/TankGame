using GameLogic;
using GameLogic.Profiles;

namespace GameTest;

public class EarningsTrackerTests
{
    // 10 ticks a second, so one cash per 10 seconds is one per 100 ticks
    private static EarningsTracker NewTracker() => new(CurrencyRates.Default, ticksPerSecond: 10);

    private static void TickFor(EarningsTracker tracker, Tank tank, int ticks)
    {
        for (var i = 0; i < ticks; i++)
            tracker.Tick([tank]);
    }

    [Fact]
    public void TimePaysOneCashPerTenSeconds()
    {
        var tracker = NewTracker();
        var tank = new Tank();
        tracker.Register(tank.Id);

        TickFor(tracker, tank, 100);

        Assert.Equal(new Earnings(Cash: 1, Seconds: 10, Hits: 0, Kills: 0), tracker.Flush(tank));
    }

    [Fact]
    public void PartOfAnIntervalCarriesOverToTheNextPayout()
    {
        var tracker = NewTracker();
        var tank = new Tank();
        tracker.Register(tank.Id);

        TickFor(tracker, tank, 60);
        Assert.Equal(new Earnings(0, 6, 0, 0), tracker.Flush(tank));
        TickFor(tracker, tank, 40);
        Assert.Equal(new Earnings(1, 4, 0, 0), tracker.Flush(tank));
    }

    [Fact]
    public void HitsAndKillsPay()
    {
        var tracker = NewTracker();
        var tank = new Tank();
        tracker.Register(tank.Id);

        var earned = tracker.Flush(tank with { HitsLanded = 3, Kills = 1 });

        Assert.Equal(new Earnings(Cash: 3 * 2 + 25, Seconds: 0, Hits: 3, Kills: 1), earned);
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

        Assert.Equal(new Earnings(2, 0, 1, 0), tracker.Flush(tank with { HitsLanded = 4 }));
    }

    [Fact]
    public void AnEliminatedTankStopsEarningTime()
    {
        var tracker = NewTracker();
        var tank = new Tank { Eliminated = true };
        tracker.Register(tank.Id);

        TickFor(tracker, tank, 100);

        Assert.True(tracker.Flush(tank).IsEmpty);
    }

    [Fact]
    public void AStoppedTankStopsEarningTime()
    {
        var tracker = NewTracker();
        var tank = new Tank();
        tracker.Register(tank.Id);
        TickFor(tracker, tank, 50);

        tracker.Stop(tank.Id);
        TickFor(tracker, tank, 100);

        Assert.Equal(new Earnings(0, 5, 0, 0), tracker.Flush(tank));
    }

    [Fact]
    public void ATankThatWasNeverRegisteredEarnsNothing()
    {
        var tracker = NewTracker();
        var tank = new Tank { HitsLanded = 5, Kills = 2 };

        TickFor(tracker, tank, 100);

        Assert.True(tracker.Flush(tank).IsEmpty);
    }
}
