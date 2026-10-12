using GameLogic;
using GameLogic.Game;
using GameLogic.Profiles;

namespace GameTest;

// The rules for earning cash, as a player would describe them: time in a match, hits landed, tanks destroyed,
// and when the money actually arrives
public class ProfileCashRulesTests
{
    private static readonly CurrencyRates Rates = CurrencyRates.Default;

    private static async Task Ticks(Game game, int count)
    {
        for (var i = 0; i < count; i++)
            await game.loopRunner.ProcessGameTick();
    }

    [Fact]
    public async Task NothingIsPaidWhileTheMatchIsStillRunning()
    {
        var store = new InMemoryProfileStore();
        var shooterProfile = await store.CreateAsync();
        var game = TestGames.NewGame(new MatchSettings { Health = 1, Lives = 5 }, profileStore: store);
        var shooter = game.JoinGame("A", shooterProfile.Id);
        var target = game.JoinGame("B");
        TestGames.ShootAt(game, shooter, target);
        await TestGames.TickUntil(game, () => game.Tanks.Single(t => t.Id == target).Deaths == 1);
        await Ticks(game, 100);

        Assert.Equal(GameStatus.Playing, game.Status);
        Assert.Equal(new Profile(shooterProfile.Id, shooterProfile.DisplayName), await store.GetAsync(shooterProfile.Id));
    }

    [Fact]
    public async Task EveryHitPaysButTheKillBonusIsPaidOnce()
    {
        var shooter = new Tank();
        var target = new Tank { Health = 3 };
        var tanks = new List<Tank> { shooter, target };
        var tracker = new EarningsTracker(CurrencyRates.Default, GameLoopRunner.TicksPerSecond);
        tracker.Register(shooter.Id);

        for (var i = 0; i < 3; i++)
            Combat.ApplyHit(tanks, 1, shooter.Id, new MatchSettings());

        Assert.Equal(new Earnings(Cash: 3 * Rates.CashPerHit + Rates.CashPerKill, Seconds: 0, Hits: 3, Kills: 1), tracker.Flush(tanks[0]));
    }

    [Fact]
    public async Task ATankThatRanOutOfLivesStopsEarningTimeWhileOthersKeepGoing()
    {
        var store = new InMemoryProfileStore();
        var (loserProfile, survivorProfile) = (await store.CreateAsync(), await store.CreateAsync());
        var game = TestGames.NewGame(new MatchSettings { Health = 1, Lives = 1 }, profileStore: store);
        var killer = game.JoinGame("Killer");
        var loser = game.JoinGame("Loser", loserProfile.Id);
        var survivor = game.JoinGame("Survivor", survivorProfile.Id);
        TestGames.ShootAt(game, killer, loser);
        await TestGames.TickUntil(game, () => game.Tanks.Single(t => t.Id == loser).Eliminated);

        await Ticks(game, 200);
        await game.PayOutAsync(loser);
        await game.PayOutAsync(survivor);

        var loserSeconds = (await store.GetAsync(loserProfile.Id))!.SecondsPlayed;
        var survivorSeconds = (await store.GetAsync(survivorProfile.Id))!.SecondsPlayed;
        Assert.True(survivorSeconds - loserSeconds >= 19, $"loser {loserSeconds}s, survivor {survivorSeconds}s");
    }

    [Fact]
    public async Task APlayerWhoLeftEarlyIsNotPaidAgainWhenTheMatchEnds()
    {
        var store = new InMemoryProfileStore();
        var winnerProfile = await store.CreateAsync();
        var game = TestGames.NewGame(new MatchSettings { Health = 1, Lives = 1 }, profileStore: store);
        var winner = game.JoinGame("A", winnerProfile.Id);
        var loser = game.JoinGame("B");
        await Ticks(game, 100);
        await game.PayOutAsync(winner);
        var afterLeaving = (await store.GetAsync(winnerProfile.Id))!;

        TestGames.ShootAt(game, winner, loser);
        await TestGames.TickUntil(game, () => game.Status == GameStatus.Ended);

        var final = (await store.GetAsync(winnerProfile.Id))!;
        Assert.Equal(10, afterLeaving.SecondsPlayed);
        Assert.Equal(afterLeaving.SecondsPlayed, final.SecondsPlayed);
        Assert.Equal(afterLeaving.Cash + Rates.CashPerHit + Rates.CashPerKill, final.Cash);
    }
}
