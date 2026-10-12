# Profiles and Currency Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Anonymous device profiles (editable display name) with an in-game cash balance earned from time played, hits landed and tanks destroyed, stored behind an interface that a real DB can implement later.

**Architecture:** A pure `EarningsTracker` turns per-tank counters (`HitsLanded`, new `Kills`) plus ticks-in-match into cash deltas. `Game` owns a tracker and a private tank-to-profile map and pays out through `IProfileStore.AwardAsync` at match end and when a player leaves. `LobbyHub` exposes profile get/create/rename; the Blazor client keeps the profile ID in `localStorage`.

**Tech Stack:** .NET 10, ASP.NET Core SignalR, Blazor WebAssembly, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-09-profiles-and-currency-design.md`

## Global Constraints

- Branch `profiles-and-currency`, based on `main`. Do **not** merge or build on `origin/jidapa`.
- `Tank.Kills` must be named exactly `Kills` (matches `origin/endscreen`, keeps the later merge conflict small).
- The profile ID is secret-like: it must **never** appear in `GameState`/`TankState` or any message sent to other clients.
- All earning numbers live in one record, `CurrencyRates`. Defaults: 1 cash per 10 seconds, 2 per hit, 25 per kill.
- Time only counts while the match is in progress (`StartedAtTick` set, status not `Ended`) and only for a tank that is not `Eliminated`.
- Names: trimmed, 1 to 20 characters, no control characters, uniqueness not required.
- Storage is async and sits behind `IProfileStore`; `AwardAsync` is an atomic delta.
- Existing `new Lobby(new FakeHubContext())` and `new Game(...)` call sites in tests must keep compiling unchanged.
- Baseline before this work: `dotnet test GameTest` = 361 passed, 0 failed.
- Every commit message ends with the trailer `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>` (pass it as a second `-m`).

## Review Focus

- A stale or unknown profile ID (server restarted, store reset) on join: the player joins anonymously with a random name and no earnings, and `GetOrCreateProfile` hands back a fresh profile instead of failing. (Tasks 2, 5)
- Name input of only whitespace, over 20 characters, or containing control characters is rejected, and a rejected rename leaves the old name. (Tasks 2, 5)
- The same profile joining one game twice (two tabs) must be refused, so one person cannot farm kills on themselves. (Task 4)
- A player who disconnects and then the match ends must not be paid twice. (Task 4)
- A creator waiting alone in a match nobody else has joined earns nothing for the waiting time. (Task 4)

---

### Task 1: Count kills

**Files:**
- Modify: `GameLogic/Tank.cs` (next to `HitsLanded`, line 48)
- Modify: `GameLogic/Combat.cs` (`ApplyHit`)
- Test: `GameTest/KillTrackingTests.cs` (create)

**Interfaces:**
- Produces: `Tank.Kills` (`int`, init-only, default 0), incremented by `Combat.ApplyHit` when the hit takes the target to 0 health and shooter != target.

- [ ] **Step 1: Write the failing tests**

Create `GameTest/KillTrackingTests.cs`:

```csharp
using GameLogic;

namespace GameTest;

public class KillTrackingTests
{
    private static (List<Tank> Tanks, Tank Shooter) Duel(int targetHealth)
    {
        var shooter = new Tank();
        var target = new Tank { Health = targetHealth };
        return ([shooter, target], shooter);
    }

    [Fact]
    public void DestroyingATankCountsAKillAndAHit()
    {
        var (tanks, shooter) = Duel(targetHealth: 1);

        Combat.ApplyHit(tanks, 1, shooter.Id, new MatchSettings());

        Assert.Equal(1, tanks[0].Kills);
        Assert.Equal(1, tanks[0].HitsLanded);
    }

    [Fact]
    public void AHitThatLeavesTheTargetAliveIsNotAKill()
    {
        var (tanks, shooter) = Duel(targetHealth: 2);

        Combat.ApplyHit(tanks, 1, shooter.Id, new MatchSettings());

        Assert.Equal(0, tanks[0].Kills);
        Assert.Equal(1, tanks[0].HitsLanded);
    }

    [Fact]
    public void ShootingYourselfIsNeitherAHitNorAKill()
    {
        var solo = new Tank { Health = 1 };
        var tanks = new List<Tank> { solo };

        Combat.ApplyHit(tanks, 0, solo.Id, new MatchSettings());

        Assert.Equal(0, tanks[0].Kills);
        Assert.Equal(0, tanks[0].HitsLanded);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test GameTest --filter "FullyQualifiedName~KillTrackingTests"`
Expected: build FAIL, `'Tank' does not contain a definition for 'Kills'`.

- [ ] **Step 3: Implement**

In `GameLogic/Tank.cs`, after the `HitsLanded` property:

```csharp
    // Tanks this one has destroyed (not counting itself); paid out as currency
    public int Kills { get; init; }
```

In `GameLogic/Combat.cs`, replace the whole of `ApplyHit` with:

```csharp
    // One hit: 1 health off the target, credited to the shooter (and a kill if it was the last health)
    public static void ApplyHit(List<Tank> tankList, int targetIndex, Guid shooterId, MatchSettings match)
    {
        var target = tankList[targetIndex];
        var health = Math.Max(0, target.Health - 1);
        tankList[targetIndex] = health > 0 ? target with { Health = health, HitFlashTicks = Tank.HitFlashTicksOnHit } : Destroy(target, match);

        // Shooting yourself with a bounce doesn't count as a hit landed
        var shooterIndex = tankList.FindIndex(tank => tank.Id == shooterId);
        if (shooterIndex >= 0 && shooterId != target.Id)
            tankList[shooterIndex] = tankList[shooterIndex] with
            {
                HitsLanded = tankList[shooterIndex].HitsLanded + 1,
                Kills = tankList[shooterIndex].Kills + (health == 0 ? 1 : 0),
            };
    }
```

- [ ] **Step 4: Run to verify it passes, then the full suite**

Run: `dotnet test GameTest`
Expected: all pass (361 + 3).

- [ ] **Step 5: Commit**

```bash
git add GameLogic/Tank.cs GameLogic/Combat.cs GameTest/KillTrackingTests.cs
git commit -m "feat: count kills on tanks" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Profile model, name rules and in-memory store

**Files:**
- Create: `GameLogic/Profiles/Profile.cs`
- Create: `GameLogic/Profiles/ProfileName.cs`
- Create: `GameLogic/Profiles/Earnings.cs` (holds `Earnings` and `CurrencyRates`)
- Create: `GameLogic/Profiles/IProfileStore.cs`
- Create: `GameLogic/Profiles/InMemoryProfileStore.cs`
- Test: `GameTest/ProfileStoreTests.cs` (create)

**Interfaces:**
- Produces:
  - `record Profile(Guid Id, string DisplayName, long Cash = 0, long SecondsPlayed = 0, int HitsLanded = 0, int Kills = 0)`
  - `static class ProfileName { const int MaxLength = 20; static bool TryNormalize(string? raw, out string name); }`
  - `record Earnings(long Cash, long Seconds, int Hits, int Kills) { bool IsEmpty }`
  - `record CurrencyRates(int SecondsPerCash = 10, int CashPerHit = 2, int CashPerKill = 25) { static readonly CurrencyRates Default }`
  - `interface IProfileStore { Task<Profile?> GetAsync(Guid id); Task<Profile> CreateAsync(); Task<Profile?> RenameAsync(Guid id, string name); Task<Profile?> AwardAsync(Guid id, Earnings earnings); }` (`RenameAsync` throws `ArgumentException` for an invalid name; both Rename/Award return `null` for an unknown id)
  - `class InMemoryProfileStore : IProfileStore`

- [ ] **Step 1: Write the failing tests**

Create `GameTest/ProfileStoreTests.cs`:

```csharp
using GameLogic.Profiles;

namespace GameTest;

public class ProfileNameTests
{
    [Theory]
    [InlineData("Rex", "Rex")]
    [InlineData("  Rex  ", "Rex")]
    [InlineData("12345678901234567890", "12345678901234567890")]
    public void ValidNamesAreTrimmed(string raw, string expected)
    {
        Assert.True(ProfileName.TryNormalize(raw, out var name));
        Assert.Equal(expected, name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123456789012345678901")]
    [InlineData("bad\nname")]
    public void InvalidNamesAreRejected(string? raw)
    {
        Assert.False(ProfileName.TryNormalize(raw, out _));
    }
}

public class InMemoryProfileStoreTests
{
    [Fact]
    public async Task CreatedProfileStartsEmptyWithAName()
    {
        var store = new InMemoryProfileStore();

        var profile = await store.CreateAsync();

        Assert.False(string.IsNullOrWhiteSpace(profile.DisplayName));
        Assert.Equal(0, profile.Cash);
        Assert.Equal(profile, await store.GetAsync(profile.Id));
    }

    [Fact]
    public async Task UnknownProfileIsNull()
    {
        var store = new InMemoryProfileStore();

        Assert.Null(await store.GetAsync(Guid.NewGuid()));
        Assert.Null(await store.RenameAsync(Guid.NewGuid(), "Rex"));
        Assert.Null(await store.AwardAsync(Guid.NewGuid(), new Earnings(5, 0, 0, 0)));
    }

    [Fact]
    public async Task RenameTrimsAndPersists()
    {
        var store = new InMemoryProfileStore();
        var profile = await store.CreateAsync();

        var renamed = await store.RenameAsync(profile.Id, "  Rex ");

        Assert.Equal("Rex", renamed!.DisplayName);
        Assert.Equal("Rex", (await store.GetAsync(profile.Id))!.DisplayName);
    }

    [Fact]
    public async Task InvalidRenameThrowsAndKeepsTheOldName()
    {
        var store = new InMemoryProfileStore();
        var profile = await store.CreateAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => store.RenameAsync(profile.Id, "   "));

        Assert.Equal(profile.DisplayName, (await store.GetAsync(profile.Id))!.DisplayName);
    }

    [Fact]
    public async Task AwardAddsToEveryTotal()
    {
        var store = new InMemoryProfileStore();
        var profile = await store.CreateAsync();

        await store.AwardAsync(profile.Id, new Earnings(Cash: 27, Seconds: 60, Hits: 1, Kills: 1));
        var after = await store.AwardAsync(profile.Id, new Earnings(Cash: 3, Seconds: 30, Hits: 2, Kills: 0));

        Assert.Equal(30, after!.Cash);
        Assert.Equal(90, after.SecondsPlayed);
        Assert.Equal(3, after.HitsLanded);
        Assert.Equal(1, after.Kills);
    }

    [Fact]
    public async Task ConcurrentAwardsAreNotLost()
    {
        var store = new InMemoryProfileStore();
        var profile = await store.CreateAsync();

        await Task.WhenAll(Enumerable.Range(0, 100)
            .Select(_ => Task.Run(() => store.AwardAsync(profile.Id, new Earnings(1, 0, 0, 0)))));

        Assert.Equal(100, (await store.GetAsync(profile.Id))!.Cash);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test GameTest --filter "FullyQualifiedName~ProfileNameTests|FullyQualifiedName~InMemoryProfileStoreTests"`
Expected: build FAIL, namespace `GameLogic.Profiles` does not exist.

- [ ] **Step 3: Implement**

`GameLogic/Profiles/Profile.cs`:

```csharp
namespace GameLogic.Profiles;

// What a player keeps between matches. The Id is the player's only credential (no login yet), so it is
// returned to its owner only and never put in game state that other players receive
public record Profile(
    Guid Id,
    string DisplayName,
    long Cash = 0,
    long SecondsPlayed = 0,
    int HitsLanded = 0,
    int Kills = 0);
```

`GameLogic/Profiles/ProfileName.cs`:

```csharp
namespace GameLogic.Profiles;

public static class ProfileName
{
    public const int MaxLength = 20;

    // Trimmed, 1 to MaxLength characters, no control characters. Not required to be unique
    public static bool TryNormalize(string? raw, out string name)
    {
        name = raw?.Trim() ?? "";
        return name.Length is >= 1 and <= MaxLength && !name.Any(char.IsControl);
    }
}
```

`GameLogic/Profiles/Earnings.cs`:

```csharp
namespace GameLogic.Profiles;

// A payout: what a player earned since their last one
public record Earnings(long Cash, long Seconds, int Hits, int Kills)
{
    public bool IsEmpty => Cash == 0 && Seconds == 0 && Hits == 0 && Kills == 0;
}

// Every earning rate in one place, so tuning the economy is a one-line change
public record CurrencyRates(int SecondsPerCash = 10, int CashPerHit = 2, int CashPerKill = 25)
{
    public static readonly CurrencyRates Default = new();
}
```

`GameLogic/Profiles/IProfileStore.cs`:

```csharp
namespace GameLogic.Profiles;

// Where profiles live. The only implementation today is in memory; a database version just implements this.
// Rename/Award return null for an unknown id. AwardAsync is a delta ("cash += x"), not a read-modify-write
public interface IProfileStore
{
    Task<Profile?> GetAsync(Guid id);
    Task<Profile> CreateAsync();
    // Throws ArgumentException when the name isn't valid (see ProfileName)
    Task<Profile?> RenameAsync(Guid id, string name);
    Task<Profile?> AwardAsync(Guid id, Earnings earnings);
}
```

`GameLogic/Profiles/InMemoryProfileStore.cs`:

```csharp
namespace GameLogic.Profiles;

// Lost on every server restart; fine until there is a database
public class InMemoryProfileStore : IProfileStore
{
    private readonly Dictionary<Guid, Profile> profiles = new();
    private readonly object gate = new();

    public Task<Profile?> GetAsync(Guid id)
    {
        lock (gate)
            return Task.FromResult(profiles.GetValueOrDefault(id));
    }

    public Task<Profile> CreateAsync()
    {
        var profile = new Profile(Guid.NewGuid(), PlayerNames.Generate([], Random.Shared));
        lock (gate)
            profiles[profile.Id] = profile;
        return Task.FromResult(profile);
    }

    public Task<Profile?> RenameAsync(Guid id, string name)
    {
        if (!ProfileName.TryNormalize(name, out var clean))
            throw new ArgumentException($"Names must be 1-{ProfileName.MaxLength} characters.", nameof(name));
        return Update(id, profile => profile with { DisplayName = clean });
    }

    public Task<Profile?> AwardAsync(Guid id, Earnings earnings) =>
        Update(id, profile => profile with
        {
            Cash = profile.Cash + earnings.Cash,
            SecondsPlayed = profile.SecondsPlayed + earnings.Seconds,
            HitsLanded = profile.HitsLanded + earnings.Hits,
            Kills = profile.Kills + earnings.Kills,
        });

    private Task<Profile?> Update(Guid id, Func<Profile, Profile> change)
    {
        lock (gate)
        {
            if (!profiles.TryGetValue(id, out var current))
                return Task.FromResult<Profile?>(null);
            return Task.FromResult<Profile?>(profiles[id] = change(current));
        }
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test GameTest --filter "FullyQualifiedName~ProfileNameTests|FullyQualifiedName~InMemoryProfileStoreTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add GameLogic/Profiles GameTest/ProfileStoreTests.cs
git commit -m "feat: add profile model and in-memory profile store" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 3: EarningsTracker

**Files:**
- Create: `GameLogic/Profiles/EarningsTracker.cs`
- Test: `GameTest/EarningsTrackerTests.cs` (create)

**Interfaces:**
- Consumes: `Tank` (`Id`, `HitsLanded`, `Kills`, `Eliminated`), `CurrencyRates`, `Earnings` (Tasks 1, 2).
- Produces: `class EarningsTracker(CurrencyRates rates, int ticksPerSecond)` with `Register(Guid tankId)`, `Tick(IEnumerable<Tank> tanks)`, `Stop(Guid tankId)`, `Earnings Flush(Tank tank)`. Not thread-safe: the caller (`Game`) holds its `StateLock`.

- [ ] **Step 1: Write the failing tests**

Create `GameTest/EarningsTrackerTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test GameTest --filter "FullyQualifiedName~EarningsTrackerTests"`
Expected: build FAIL, `EarningsTracker` not found.

- [ ] **Step 3: Implement**

`GameLogic/Profiles/EarningsTracker.cs`:

```csharp
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
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test GameTest --filter "FullyQualifiedName~EarningsTrackerTests"`
Expected: PASS (7 tests).

- [ ] **Step 5: Commit**

```bash
git add GameLogic/Profiles/EarningsTracker.cs GameTest/EarningsTrackerTests.cs
git commit -m "feat: add earnings tracker" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Game pays players out

**Files:**
- Modify: `GameLogic/Game.cs` (usings, new members, `JoinGame`)
- Modify: `GameLogic/GameLoopRunner.cs` (tick + payout call)
- Modify: `GameLogic/Lobby.cs` (store, `Profiles`, `ResolvePlayerAsync`, pass store to games)
- Modify: `GameTest/TestSupport.cs` (`NewGame` gains `profileStore`)
- Test: `GameTest/ProfileEarningsTests.cs` (create)

**Interfaces:**
- Consumes: `IProfileStore`, `EarningsTracker`, `CurrencyRates`, `Earnings`, `Tank.Kills`.
- Produces:
  - `Game.ProfileStore` (`IProfileStore?`, init-only; null = nobody is paid)
  - `Game.Tracker` (`internal EarningsTracker`, init-only; defaults to `CurrencyRates.Default` at `GameLoopRunner.TicksPerSecond`)
  - `Game.JoinGame(string? playerName = null, Guid? profileId = null)` (throws `InvalidOperationException` if that profile already has a tank in this game)
  - `Task Game.PayOutAsync(Guid? tankId = null)`: with an id, pays that player and stops their time; with none, pays everyone once (match end)
  - `Lobby(IHubContext<LobbyHub> context, IMapSource? mapSource = null, IProfileStore? profileStore = null)`, `Lobby.Profiles` (`IProfileStore`), `Task<(string? Name, Guid? ProfileId)> Lobby.ResolvePlayerAsync(string? playerName, Guid? profileId)`
  - `TestGames.NewGame(..., IProfileStore? profileStore = null)` (new last parameter)

- [ ] **Step 1: Write the failing tests**

First add the parameter to `GameTest/TestSupport.cs`. Replace the `NewGame` helper with:

```csharp
    public static Game NewGame(MatchSettings? settings = null, FakeClock? clock = null,
        string matchType = GameMatchTypes.Multiplayer, Random? botRandom = null, IProfileStore? profileStore = null) =>
        new(new FakeHubContext())
        {
            Map = Arena,
            MatchType = matchType,
            SpawnRandom = new FirstSpawnRandom(),
            BotRandom = botRandom ?? new Random(1),
            Settings = settings ?? new MatchSettings(),
            Clock = (clock ?? new FakeClock()).Now,
            ProfileStore = profileStore,
        };
```

and add `using GameLogic.Profiles;` at the top of that file.

Create `GameTest/ProfileEarningsTests.cs`:

```csharp
using System.Text.Json;
using GameLogic;
using GameLogic.Game;
using GameLogic.Profiles;

namespace GameTest;

public class ProfileEarningsTests
{
    // One shot ends it: 1 health, 1 life. Winner gets 1 hit and 1 kill = 2 + 25 cash, plus a little time
    private static async Task<(Game Game, InMemoryProfileStore Store, Profile Winner, Profile Loser)> FinishedDuel()
    {
        var store = new InMemoryProfileStore();
        var winner = await store.CreateAsync();
        var loser = await store.CreateAsync();
        var game = TestGames.NewGame(new MatchSettings { Health = 1, Lives = 1 }, profileStore: store);
        var a = game.JoinGame("A", winner.Id);
        var b = game.JoinGame("B", loser.Id);
        TestGames.ShootAt(game, a, b);
        await TestGames.TickUntil(game, () => game.Status == GameStatus.Ended);
        return (game, store, winner, loser);
    }

    [Fact]
    public async Task MatchEndPaysEveryProfileForHitsKillsAndTime()
    {
        var (_, store, winnerBefore, loserBefore) = await FinishedDuel();

        var winner = (await store.GetAsync(winnerBefore.Id))!;
        var loser = (await store.GetAsync(loserBefore.Id))!;

        Assert.Equal(1, winner.HitsLanded);
        Assert.Equal(1, winner.Kills);
        Assert.Equal(27 + winner.SecondsPlayed / 10, winner.Cash);
        Assert.Equal(0, loser.HitsLanded);
        Assert.Equal(0, loser.Kills);
        Assert.Equal(loser.SecondsPlayed / 10, loser.Cash);
    }

    [Fact]
    public async Task MatchEndPaysOnlyOnce()
    {
        var (game, store, winnerBefore, _) = await FinishedDuel();
        var paid = (await store.GetAsync(winnerBefore.Id))!;

        await game.PayOutAsync();
        await game.PayOutAsync(game.Tanks.First().Id);

        Assert.Equal(paid, await store.GetAsync(winnerBefore.Id));
    }

    [Fact]
    public async Task APlayerWhoLeavesIsPaidAtOnceAndNotAgain()
    {
        var store = new InMemoryProfileStore();
        var shooterProfile = await store.CreateAsync();
        // 1 health but 5 lives: the kill lands and the match carries on
        var game = TestGames.NewGame(new MatchSettings { Health = 1, Lives = 5 }, profileStore: store);
        var shooter = game.JoinGame("A", shooterProfile.Id);
        var target = game.JoinGame("B");
        TestGames.ShootAt(game, shooter, target);
        await TestGames.TickUntil(game, () => game.Tanks.Single(t => t.Id == target).Deaths == 1);

        await game.PayOutAsync(shooter);
        var afterLeaving = (await store.GetAsync(shooterProfile.Id))!;
        await game.PayOutAsync(shooter);

        Assert.Equal(1, afterLeaving.Kills);
        Assert.True(afterLeaving.Cash >= 27);
        Assert.Equal(afterLeaving, await store.GetAsync(shooterProfile.Id));
    }

    [Fact]
    public async Task WaitingAloneEarnsNoTime()
    {
        var store = new InMemoryProfileStore();
        var profile = await store.CreateAsync();
        var game = TestGames.NewGame(profileStore: store);
        var id = game.JoinGame("A", profile.Id);

        for (var i = 0; i < 150; i++)
            await game.loopRunner.ProcessGameTick();
        await game.PayOutAsync(id);

        var after = (await store.GetAsync(profile.Id))!;
        Assert.Equal(0, after.SecondsPlayed);
        Assert.Equal(0, after.Cash);
    }

    [Fact]
    public async Task APlayerWithoutAProfileCanStillPlayAMatchThroughToTheEnd()
    {
        var store = new InMemoryProfileStore();
        var profile = await store.CreateAsync();
        var game = TestGames.NewGame(new MatchSettings { Health = 1, Lives = 1 }, profileStore: store);
        var anonymous = game.JoinGame("A");
        var registered = game.JoinGame("B", profile.Id);

        TestGames.ShootAt(game, anonymous, registered);
        await TestGames.TickUntil(game, () => game.Status == GameStatus.Ended);

        Assert.Equal(GameStatus.Ended, game.Status);
        Assert.Equal(0, (await store.GetAsync(profile.Id))!.Kills);
    }

    [Fact]
    public async Task AGameWithoutAStoreIgnoresProfiles()
    {
        var game = TestGames.NewGame();
        var id = game.JoinGame("A", Guid.NewGuid());

        await game.PayOutAsync(id);
        await game.PayOutAsync();
    }

    [Fact]
    public async Task OneProfileCannotHaveTwoTanksInTheSameGame()
    {
        var store = new InMemoryProfileStore();
        var profile = await store.CreateAsync();
        var game = TestGames.NewGame(profileStore: store);
        game.JoinGame("A", profile.Id);

        Assert.Throws<InvalidOperationException>(() => game.JoinGame("A again", profile.Id));
        Assert.Single(game.Tanks);
    }

    [Fact]
    public async Task TheProfileIdNeverReachesAnyViewer()
    {
        var store = new InMemoryProfileStore();
        var mine = await store.CreateAsync();
        var theirs = await store.CreateAsync();
        var game = TestGames.NewGame(profileStore: store);
        var me = game.JoinGame("A", mine.Id);
        var them = game.JoinGame("B", theirs.Id);

        foreach (var viewer in new Guid?[] { me, them, null })
        {
            var json = JsonSerializer.Serialize(game.GetGameState(viewerId: viewer));
            Assert.DoesNotContain(mine.Id.ToString(), json);
            Assert.DoesNotContain(theirs.Id.ToString(), json);
        }
    }
}

public class LobbyProfileTests
{
    [Fact]
    public async Task AKnownProfileSuppliesTheNameAndIsKept()
    {
        var store = new InMemoryProfileStore();
        var profile = await store.RenameAsync((await store.CreateAsync()).Id, "Rex");
        var lobby = new Lobby(new FakeHubContext(), profileStore: store);

        var (name, profileId) = await lobby.ResolvePlayerAsync("ignored", profile!.Id);

        Assert.Equal("Rex", name);
        Assert.Equal(profile.Id, profileId);
    }

    [Fact]
    public async Task AnUnknownProfileJoinsAnonymouslyWithTheTypedName()
    {
        var lobby = new Lobby(new FakeHubContext());

        var (name, profileId) = await lobby.ResolvePlayerAsync("Typed", Guid.NewGuid());

        Assert.Equal("Typed", name);
        Assert.Null(profileId);
    }

    [Fact]
    public async Task NoProfileJoinsAnonymously()
    {
        var lobby = new Lobby(new FakeHubContext());

        var (name, profileId) = await lobby.ResolvePlayerAsync(null, null);

        Assert.Null(name);
        Assert.Null(profileId);
    }

    [Fact]
    public void GamesCreatedByTheLobbyPayIntoTheLobbysStore()
    {
        var lobby = new Lobby(new FakeHubContext());

        var game = lobby.CreateGame("g");

        Assert.Same(lobby.Profiles, game.ProfileStore);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test GameTest --filter "FullyQualifiedName~ProfileEarningsTests|FullyQualifiedName~LobbyProfileTests"`
Expected: build FAIL (`ProfileStore`, `PayOutAsync`, `ResolvePlayerAsync`, `Lobby.Profiles` missing).

- [ ] **Step 3: Implement `Game`**

In `GameLogic/Game.cs`, add `using GameLogic.Profiles;` to the usings.

Add these members inside `Game`, right after the `botBrains` field:

```csharp
    // Where earnings go; null means nobody is paid (tests, or no store configured)
    public IProfileStore? ProfileStore { get; init; }
    // Money rules live in the tracker; tests can swap in different rates
    internal EarningsTracker Tracker { get; init; } = new(CurrencyRates.Default, GameLoopRunner.TicksPerSecond);
    // Tank id -> the profile it plays for. Private on purpose: a profile id is a credential and is never sent to clients
    private readonly Dictionary<Guid, Guid> profileByTank = new();
    private bool matchPaidOut;
```

Replace `JoinGame` with:

```csharp
    // A blank name (quick join, or no name set) gets a generated one that no one else in the game has.
    // A profile id makes the tank earn currency for that profile; a profile can only have one tank per game
    public Guid JoinGame(string? playerName = null, Guid? profileId = null)
    {
        lock (StateLock)
        {
            if (Status == GameStatus.Ended)
                throw new InvalidOperationException($"cannot join game, it has ended: {Name}");
            // A match with bots is the creator's alone; the bots fill the other seats
            if (MatchType == GameMatchTypes.Bots && HumanCount >= 1)
                throw new InvalidOperationException($"cannot join game, it is single player: {Name}");
            if (Tanks.Count() >= Map.MaxPlayers)
                throw new InvalidOperationException($"cannot join game, lobby is full: {Name}");
            if (profileId is { } taken && profileByTank.ContainsValue(taken))
                throw new InvalidOperationException($"cannot join game, this profile is already playing in it: {Name}");

            var id = AddTank(playerName, isBot: false);
            if (profileId is { } profile)
            {
                profileByTank[id] = profile;
                Tracker.Register(id);
            }
            CreatorId ??= id;
            // Bots arrive with the creator in single player and with the 2nd human in multiplayer; until then nobody
            // would be fighting them. Their seats come out of the same limit as everyone's, so humans can't take them
            if (!botsJoined && BotSlots > 0 && HumanCount == (MatchType == GameMatchTypes.Bots ? 1 : 2))
            {
                botsJoined = true;
                for (var i = 0; i < BotSlots; i++)
                    AddTank(null, isBot: true);
            }
            return id;
        }
    }

    // With a tank id: that player is leaving, so pay what they have earned and stop their clock.
    // Without: the match is over, pay everyone (once). Paying again never repeats a payout
    public async Task PayOutAsync(Guid? tankId = null)
    {
        if (ProfileStore is null)
            return;

        List<(Guid ProfileId, Earnings Earned)> due = [];
        lock (StateLock)
        {
            if (tankId is null)
            {
                if (matchPaidOut)
                    return;
                matchPaidOut = true;
            }
            foreach (var tank in Tanks)
            {
                if (tankId is { } only && tank.Id != only)
                    continue;
                if (!profileByTank.TryGetValue(tank.Id, out var profileId))
                    continue;
                var earned = Tracker.Flush(tank);
                if (tankId is not null)
                    Tracker.Stop(tank.Id);
                if (!earned.IsEmpty)
                    due.Add((profileId, earned));
            }
        }

        // A failing store must not take the game loop down with it
        foreach (var (profileId, earned) in due)
        {
            try { await ProfileStore.AwardAsync(profileId, earned); }
            catch (Exception ex) { Console.WriteLine($"Could not pay {profileId} in game {Name}: {ex}"); }
        }
    }
```

(Delete the old `JoinGame` method; this replaces it.)

- [ ] **Step 4: Implement the game loop hook**

In `GameLogic/GameLoopRunner.cs`, directly after `game.Tick++;` add:

```csharp
            // Time only pays once the match is on (a creator waiting alone earns nothing)
            if (game.StartedAtTick is not null)
                game.Tracker.Tick(game.Tanks);
```

and after the closing brace of the `lock (game.StateLock) { ... }` block (just before `game.ServerWorkMs = ...`), add:

```csharp
        if (game.Status == GameStatus.Ended)
            await game.PayOutAsync();
```

- [ ] **Step 5: Implement `Lobby`**

Replace `GameLogic/Lobby.cs` with:

```csharp
using System.Collections.Concurrent;
using GameLogic;
using GameLogic.Game;
using GameLogic.Profiles;
using Microsoft.AspNetCore.SignalR;

public class Lobby
{
  public List<Game> Games { get; set; } = new();
  public IProfileStore Profiles { get; }
  private readonly IHubContext<LobbyHub> context;
  private readonly IMapSource mapSource;

  //public event Action? OnLobbyUpdate;
  public Lobby(IHubContext<LobbyHub> context, IMapSource? mapSource = null, IProfileStore? profileStore = null)
  {
    this.context = context;
    this.mapSource = mapSource ?? new FixedMapSource();
    Profiles = profileStore ?? new InMemoryProfileStore();
  }

  // A known profile decides the player's name and earns currency; an unknown or missing one (for example after a
  // server restart) joins anonymously with the name that was typed, if any
  public async Task<(string? Name, Guid? ProfileId)> ResolvePlayerAsync(string? playerName, Guid? profileId)
  {
    if (profileId is { } id && await Profiles.GetAsync(id) is { } profile)
      return (profile.DisplayName, id);
    return (playerName, null);
  }

  public Game CreateGame(string name, string? mapName = null, string? matchType = null, MatchSettings? settings = null)
  {
    var type = matchType switch
    {
      GameMatchTypes.DeveloperSimulation => GameMatchTypes.DeveloperSimulation,
      GameMatchTypes.Bots => GameMatchTypes.Bots,
      _ => GameMatchTypes.Multiplayer
    };
    var map = mapSource.GetByName(mapName);

    var chosen = MatchSettings.Sanitize(settings ?? new MatchSettings());

    var newGame = new Game(context)
    {
      Name = name,
      MatchType = type,
      Map = map,
      Settings = chosen,
      ProfileStore = Profiles
    };

    Games.Add(newGame);
    return newGame;
  }
}
```

- [ ] **Step 6: Run to verify it passes, then the full suite**

Run: `dotnet test GameTest`
Expected: all pass. If `MatchEndPaysEveryProfileForHitsKillsAndTime` fails on the cash figure, print `winner` and check the time term (`SecondsPlayed / 10`) rather than loosening the 27.

- [ ] **Step 7: Commit**

```bash
git add GameLogic/Game.cs GameLogic/GameLoopRunner.cs GameLogic/Lobby.cs GameTest/TestSupport.cs GameTest/ProfileEarningsTests.cs
git commit -m "feat: pay profiles for time, hits and kills" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Hub methods and server registration

**Files:**
- Modify: `GameLogic/models/Messages.cs`
- Modify: `GameLogic/LobbyHub.cs`
- Test: `GameTest/ProfileHubTests.cs` (create)

**Interfaces:**
- Consumes: `Lobby.Profiles`, `Lobby.ResolvePlayerAsync`, `Game.JoinGame(name, profileId)`, `Game.PayOutAsync(tankId)`, `ProfileName.TryNormalize`.
- Produces (hub methods the client calls):
  - `Task<Profile> GetOrCreateProfile(Guid? profileId)`: returns the existing profile, or a **new** one when the id is missing or unknown
  - `Task<Profile> RenameProfile(Guid profileId, string name)`: throws `HubException` for an invalid name or unknown profile
  - `CreateGame(string name, string? mapName, string? matchType, MatchSettings? settings, string? playerName = null, Guid? profileId = null)`
  - `JoinGame(string gameName, string? playerName = null, Guid? profileId = null)`
  - `Messages.GetOrCreateProfile`, `Messages.RenameProfile`

No change to `GameApi/Program.cs` is needed: `Lobby` already registers as a singleton and builds its own `InMemoryProfileStore`. The line to change when a DB arrives is the `Lobby` constructor default (or register the real `IProfileStore` and inject it).

- [ ] **Step 1: Write the failing tests**

Create `GameTest/ProfileHubTests.cs`:

```csharp
using GameLogic;
using GameLogic.Profiles;
using Microsoft.AspNetCore.SignalR;

namespace GameTest;

public class ProfileHubTests
{
    private static LobbyHub NewHub() => new(new Lobby(new FakeHubContext()));

    [Fact]
    public async Task NoIdCreatesAProfile()
    {
        var profile = await NewHub().GetOrCreateProfile(null);

        Assert.NotEqual(Guid.Empty, profile.Id);
        Assert.False(string.IsNullOrWhiteSpace(profile.DisplayName));
    }

    [Fact]
    public async Task AKnownIdReturnsTheSameProfile()
    {
        var hub = NewHub();
        var created = await hub.GetOrCreateProfile(null);

        Assert.Equal(created, await hub.GetOrCreateProfile(created.Id));
    }

    [Fact]
    public async Task AnUnknownIdGetsAFreshProfileInsteadOfAnError()
    {
        var staleId = Guid.NewGuid();

        var profile = await NewHub().GetOrCreateProfile(staleId);

        Assert.NotEqual(staleId, profile.Id);
    }

    [Fact]
    public async Task RenameChangesTheName()
    {
        var hub = NewHub();
        var profile = await hub.GetOrCreateProfile(null);

        var renamed = await hub.RenameProfile(profile.Id, "  Rex ");

        Assert.Equal("Rex", renamed.DisplayName);
        Assert.Equal("Rex", (await hub.GetOrCreateProfile(profile.Id)).DisplayName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123456789012345678901")]
    public async Task ARejectedRenameKeepsTheOldName(string badName)
    {
        var hub = NewHub();
        var profile = await hub.GetOrCreateProfile(null);

        await Assert.ThrowsAsync<HubException>(() => hub.RenameProfile(profile.Id, badName));

        Assert.Equal(profile.DisplayName, (await hub.GetOrCreateProfile(profile.Id)).DisplayName);
    }

    [Fact]
    public async Task RenamingAnUnknownProfileIsRejected()
    {
        await Assert.ThrowsAsync<HubException>(() => NewHub().RenameProfile(Guid.NewGuid(), "Rex"));
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test GameTest --filter "FullyQualifiedName~ProfileHubTests"`
Expected: build FAIL (`GetOrCreateProfile` missing).

- [ ] **Step 3: Implement**

In `GameLogic/models/Messages.cs`, add inside the class:

```csharp
  public static readonly string GetOrCreateProfile = "GetOrCreateProfile";
  public static readonly string RenameProfile = "RenameProfile";
```

In `GameLogic/LobbyHub.cs`: add `using GameLogic.Profiles;` to the usings, then make these changes.

Add after `PerformancePing`:

```csharp
  // The id comes from the player's browser. A missing or unknown one (first visit, or the server restarted) gets a new profile;
  // the client stores whatever id comes back. The profile is returned to the caller only
  public async Task<Profile> GetOrCreateProfile(Guid? profileId)
  {
    if (profileId is { } id && await lobby.Profiles.GetAsync(id) is { } existing)
      return existing;
    return await lobby.Profiles.CreateAsync();
  }

  public async Task<Profile> RenameProfile(Guid profileId, string name)
  {
    if (!ProfileName.TryNormalize(name, out var clean))
      throw new HubException($"Names must be 1-{ProfileName.MaxLength} characters.");
    return await lobby.Profiles.RenameAsync(profileId, clean)
      ?? throw new HubException("Profile not found. Reload the page to start a new one.");
  }
```

Replace `CreateGame` and `JoinGame` with:

```csharp
  // SignalR doesn't fill optional parameters, so clients must send every argument
  public async Task CreateGame(string name, string? mapName = null, string? matchType = null, MatchSettings? settings = null, string? playerName = null, Guid? profileId = null)
  {
    var nameTaken = lobby.Games.FirstOrDefault(g => g.Name == name) != null;
    if(nameTaken)
    {
      throw new Exception($"cannot create game, name already taken: {name}");
    }

    var game = lobby.CreateGame(name, mapName, matchType, settings);
    Console.WriteLine($"created game: {name}");

    var (resolvedName, resolvedProfile) = await lobby.ResolvePlayerAsync(playerName, profileId);
    var playerId = game.JoinGame(resolvedName, resolvedProfile);

    await Clients.Client(Context.ConnectionId).SendAsync(Messages.CreatedGame, game.Name, playerId);
    game.loopRunner.RunGameLoop();

    var games = lobby.Games.Select(g => g.GetGameState()).ToArray();
    await Clients.All.SendAsync(Messages.GameList, games);
  }

  public async Task JoinGame(string gameName, string? playerName = null, Guid? profileId = null)
  {
    var game = lobby.Games.FirstOrDefault(g => g.Name == gameName)
      ?? throw new HubException($"Battle '{gameName}' is no longer available. Return to the lobby to create or join a battle.");
    var (resolvedName, resolvedProfile) = await lobby.ResolvePlayerAsync(playerName, profileId);
    var playerId = game.JoinGame(resolvedName, resolvedProfile);
    await SubscribeToGame(gameName, playerId);
    await Clients.Client(Context.ConnectionId).SendAsync(Messages.JoinedGame, game.Name, playerId);
    await Clients.All.SendAsync(Messages.GameList, lobby.Games.Select(g => g.GetGameState()).ToArray());
  }
```

Replace `UnsubscribeFromGame` with:

```csharp
  public async Task UnsubscribeFromGame(string gameName)
  {
    var game = lobby.Games.FirstOrDefault(g => g.Name == gameName);
    if (game is not null && game.ConnectedClients.TryRemove(Context.ConnectionId, out var playerId) && playerId is { } id)
      await game.PayOutAsync(id);
  }
```

In `OnDisconnectedAsync`, replace the loop body so a leaving player is paid:

```csharp
    foreach (var game in lobby.Games)
    {
      if (game.ConnectedClients.TryRemove(connectionId, out var playerId))
      {
        Console.WriteLine($"Removed connection: {connectionId} from game {game.Name}");
        if (playerId is { } id)
          await game.PayOutAsync(id);
      }
    }
```

- [ ] **Step 4: Run to verify it passes, then the full suite**

Run: `dotnet test GameTest`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add GameLogic/models/Messages.cs GameLogic/LobbyHub.cs GameTest/ProfileHubTests.cs
git commit -m "feat: profile hub methods and pay players who leave" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Client profile service and lobby panel

No automated tests exist for the Blazor pages; this task is verified by building and by the manual check at the end.

**Files:**
- Create: `GameFrontend/ProfileService.cs`
- Modify: `GameFrontend/Program.cs`
- Modify: `GameFrontend/Pages/Home.razor` (panel markup lines 69-78, `@code`, `CreateGame`, `JoinGame`)
- Modify: `GameFrontend/Pages/Home.razor.css` (append)
- Modify: `GameFrontend/Pages/GamePage.razor:451`
- Modify: `GameFrontend/DebugBridge.cs:29`

**Interfaces:**
- Consumes: hub methods from Task 5, `Messages.GetOrCreateProfile`, `Messages.RenameProfile`, `SignalRService`.
- Produces: `ProfileService` (`Profile? Current`, `Task<Profile> LoadAsync()`, `Task<Profile> RenameAsync(string name)`).

- [ ] **Step 1: Create the service**

`GameFrontend/ProfileService.cs`:

```csharp
using GameLogic;
using GameLogic.Profiles;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;

namespace GameFrontend;

// The player's profile: its id lives in this browser's localStorage, everything else on the server
public class ProfileService(SignalRService signalR, IJSRuntime js)
{
    private const string StorageKey = "tankgame.profileId";

    public Profile? Current { get; private set; }

    // Asks the server for the saved profile (also how the cash balance is refreshed). A first visit, or an id the
    // server no longer knows, comes back as a brand-new profile whose id replaces the saved one
    public async Task<Profile> LoadAsync()
    {
        await signalR.EnsureConnected();
        var saved = Current?.Id ?? await ReadSavedId();
        Current = await signalR.HubConnection!.InvokeAsync<Profile>(Messages.GetOrCreateProfile, saved);
        if (Current.Id != saved)
            await SaveId(Current.Id);
        return Current;
    }

    public async Task<Profile> RenameAsync(string name)
    {
        var profile = Current ?? await LoadAsync();
        Current = await signalR.HubConnection!.InvokeAsync<Profile>(Messages.RenameProfile, profile.Id, name);
        return Current;
    }

    // Storage can be blocked (private windows); the profile then lasts until the page is closed
    private async Task<Guid?> ReadSavedId()
    {
        try { return Guid.TryParse(await js.InvokeAsync<string?>("localStorage.getItem", StorageKey), out var id) ? id : null; }
        catch (JSException) { return null; }
    }

    private async Task SaveId(Guid id)
    {
        try { await js.InvokeVoidAsync("localStorage.setItem", StorageKey, id.ToString()); }
        catch (JSException) { }
    }
}
```

In `GameFrontend/Program.cs`, after the `AddSingleton(new SignalRService(...))` line add:

```csharp
builder.Services.AddScoped<ProfileService>();
```

- [ ] **Step 2: Update the lobby panel**

In `GameFrontend/Pages/Home.razor`, add `@inject ProfileService profiles` under the existing `@inject SignalRService signalR;` line.

Replace the `<section class="panel"> ... </section>` block (the "Your name" panel, lines 69-78) with:

```razor
  <section class="panel profile-panel">
    <div class="profile-name">
      <span class="field-label">Your profile</span>
      <div class="name-input @(profileError is null ? "" : "has-error")">
        <input type="text" placeholder="Callsign" maxlength="20" autocomplete="off"
               @bind=nameDraft @bind:event="oninput" />
        <button class="btn-game" disabled="@(!NameChanged)" @onclick="SaveName">Save</button>
      </div>
      @if (profileError is not null)
      {
        <span class="field-error">@profileError</span>
      }
    </div>
    <div class="profile-cash">
      <span class="profile-cash-value">@(profile?.Cash ?? 0)</span>
      <span class="profile-cash-label">Cash</span>
    </div>
  </section>
```

In the `@code` block, replace `private static string playerName = "";` (and its preceding comment line) with:

```csharp
  private Profile? profile;
  private string nameDraft = "";
  private string? profileError;
  private bool NameChanged => profile is not null && nameDraft.Trim() != profile.DisplayName;

  private async Task SaveName()
  {
    profileError = null;
    try
    {
      profile = await profiles.RenameAsync(nameDraft);
      nameDraft = profile.DisplayName;
    }
    catch (Exception ex) when (ex is HubException || ex.GetBaseException() is HubException)
    {
      profileError = "Names need 1 to 20 characters.";
    }
    catch
    {
      profileError = "Couldn't save your name. Try again.";
    }
  }
```

(`HubException` is `Microsoft.AspNetCore.SignalR.HubException`, already covered by the file's `@using Microsoft.AspNetCore.SignalR.Client`? It is not: add `@using Microsoft.AspNetCore.SignalR` and `@using GameLogic.Profiles` to the top of `Home.razor`. Client-side, hub errors surface as `HubException` from the client package, which lives in `Microsoft.AspNetCore.SignalR`.)

In `OnInitializedAsync`, directly after `await signalR.EnsureConnected();` add:

```csharp
    try
    {
      profile = await profiles.LoadAsync();
      nameDraft = profile.DisplayName;
    }
    catch (Exception ex)
    {
      Console.WriteLine(ex);
      profileError = "Couldn't load your profile.";
    }
```

In `CreateGame`, change the invoke line to:

```csharp
        await connection.InvokeAsync(Messages.CreateGame, userInputGameName.Trim(), selectedMapName, selectedMatchType, settings, "", profile?.Id);
```

In `JoinGame`, change the invoke line to:

```csharp
        await connection.InvokeAsync(Messages.JoinGame, gameName, "", profile?.Id);
```

- [ ] **Step 3: Style the panel**

Append to `GameFrontend/Pages/Home.razor.css`:

```css
.profile-panel {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 1.5rem;
}

.profile-name {
  flex: 1;
  min-width: 0;
}

.profile-cash {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
}

.profile-cash-value {
  font-size: 1.75rem;
  font-weight: 700;
  line-height: 1;
}

.profile-cash-label {
  font-size: 0.75rem;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  opacity: 0.7;
}
```

- [ ] **Step 4: Keep the other JoinGame callers working**

The hub now takes a third `JoinGame` argument and SignalR needs every argument. In `GameFrontend/Pages/GamePage.razor` line 451 change to:

```csharp
    await signalR.HubConnection?.SendAsync(Messages.JoinGame, GameName, "", null)!;
```

In `GameFrontend/DebugBridge.cs` change `Join` to:

```csharp
  public static Task Join(string gameName) =>
    Service?.HubConnection?.SendAsync(Messages.JoinGame, gameName, "", null) ?? Task.CompletedTask;
```

- [ ] **Step 5: Build everything**

Run: `dotnet build`
Expected: Build succeeded, 0 errors. If `SendAsync(..., null)` is ambiguous, use `(Guid?)null`. If the `HubException` catch does not compile, replace the first `catch` with `catch (Exception ex) when (ex.Message.Contains("characters"))`.

- [ ] **Step 6: Commit**

```bash
git add GameFrontend
git commit -m "feat: profile panel in the lobby" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Docs and end-to-end check

**Files:**
- Modify: `docs/superpowers/specs/2026-10-09-profiles-and-currency-design.md`
- Modify: `Readme.md`

- [ ] **Step 1: Bring the spec in line with what was built**

In the spec: under "Profile and store" change "`ConcurrentDictionary`-backed" to "lock-guarded dictionary", and under "Earnings" add the bullet "A profile can have only one tank per game, so a player cannot farm kills on themselves with two tabs."

- [ ] **Step 2: Add a Readme note**

Append to `Readme.md`:

```markdown
- players have a profile (name and cash) saved in their browser; cash comes from time in a match, hits landed and tanks destroyed
- profiles are kept in memory for now (`InMemoryProfileStore`) and reset when the server restarts; to use a database, implement `IProfileStore` and pass it to `Lobby`
- earning rates live in one place: `CurrencyRates` (`GameLogic/Profiles/Earnings.cs`)
```

- [ ] **Step 3: Full verification**

Run: `dotnet build` then `dotnet test GameTest`
Expected: 0 errors; all tests pass (361 baseline + the new ones, 0 failed).

Manual check (API on :5135, frontend on :3000, see `launchSettings.json`): run both projects, then in a browser:
1. Open the lobby: the profile panel shows a random callsign and `Cash 0`. Reload: the same callsign.
2. Rename to "Rex" and Save: the name sticks after reload. Try a blank name and a 21-character name: an error shows and the old name stays.
3. Create a "With bots" battle, play until you hit and destroy a bot, then leave the match. Back in the lobby the cash is at least 27.
4. Restart the API, reload the lobby: a new profile with 0 cash appears (expected: profiles are in memory).

- [ ] **Step 4: Commit**

```bash
git add docs Readme.md
git commit -m "docs: describe profiles and currency" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Self-review notes

- **Spec coverage:** profile/store/name rules (Task 2); earnings and `CurrencyRates` (Tasks 2, 3); kills (Task 1); payout at match end and on leaving, private tank-to-profile map, profile id never in state (Task 4); hub methods and `profileId` arguments, profile name overriding the random one (Tasks 4, 5); `localStorage` `ProfileService` and lobby panel with cash (Task 6); testing (each task). The spec's "one tank per profile per game" rule is documented in Task 7.
- **Cash after a match:** the balance is refreshed whenever the lobby loads (`LoadAsync`), which is how a payout shows up when the player returns.
- **Known limitations kept from the spec:** profiles reset on server restart; time stops counting for a player after they leave the game page and later rejoin the same tank (hits and kills still pay at match end); a failed store write loses that payout (logged).
