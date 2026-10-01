using System.Diagnostics;
using System.Text.Json;
using GameLogic;
using GameLogic.Game;
using Xunit.Abstractions;

namespace GameTest;

public class BotMatchTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> StandardMaps() =>
        MapCatalog.FixedMaps.Where(map => map.Mode == MapMode.Standard).Select(map => new object[] { map.Name });

    // Catches a bot that gets stuck on a wall for good, or never manages to shoot
    [Theory]
    [MemberData(nameof(StandardMaps))]
    public async Task AHardBotBeatsAStationaryHumanOnEveryStandardMap(string mapName)
    {
        var clock = new FakeClock();
        var game = new Game(new FakeHubContext())
        {
            Map = MapCatalog.GetByName(mapName),
            MatchType = GameMatchTypes.Bots,
            Settings = new MatchSettings
            {
                BotCount = 1, BotDifficulty = BotDifficulty.Hard, Health = 3, Lives = 1, ReloadMs = 500, MaxBounces = 1,
            },
            Clock = clock.Now,
            SpawnRandom = new Random(7),
            BotRandom = new Random(7),
        };
        var human = game.JoinGame();

        // Two minutes of game time; the clock moves with the ticks so reloads finish
        var ticks = 0;
        while (game.Status != GameStatus.Ended && ticks < 1200)
        {
            clock.Advance(100);
            await game.loopRunner.ProcessGameTick();
            ticks++;
        }

        output.WriteLine($"{mapName}: ended after {ticks} ticks");
        Assert.Equal(GameStatus.Ended, game.Status);
        Assert.True(game.GetGameState().BotsWon);
        Assert.True(game.Tanks.Single(t => t.Id == human).Eliminated);
    }

    [Fact]
    public async Task SevenBotsStayWithinTheTickBudget()
    {
        var clock = new FakeClock();
        var game = new Game(new FakeHubContext())
        {
            Map = MapCatalog.FixedMaps.First(map => map.Mode == MapMode.Standard),
            MatchType = GameMatchTypes.Bots,
            Settings = new MatchSettings { BotCount = 7, BotDifficulty = BotDifficulty.Hard, Health = 10, Lives = 10 },
            Clock = clock.Now,
            SpawnRandom = new FirstSpawnRandom(),
            BotRandom = new Random(3),
        };
        var human = game.JoinGame();
        var durations = new List<double>();
        var botBullets = new HashSet<Guid>();

        for (var tick = 0; tick < 120; tick++)
        {
            clock.Advance(100);
            var at = Stopwatch.GetTimestamp();
            await game.loopRunner.ProcessGameTick();
            foreach (var bullet in game.Bullets.Where(bullet => bullet.OwnerId != human))
                botBullets.Add(bullet.Id);
            _ = JsonSerializer.SerializeToUtf8Bytes(game.GetGameState(includeMap: false));
            if (tick >= 20)
                durations.Add(Stopwatch.GetElapsedTime(at).TotalMilliseconds);
        }

        durations.Sort();
        var p95 = durations[(int)(durations.Count * .95)];
        output.WriteLine($"7 bots: simulation + snapshot serialization p95={p95:F2}ms, max={durations[^1]:F2}ms");
        Assert.Equal(8, game.Tanks.Count());
        // The timing only means something if the bots were really acting the whole time
        Assert.False(game.Tanks.Single(t => t.Id == human).Eliminated, "the human went down, so the match may have ended early");
        Assert.NotEmpty(botBullets);
        Assert.True(p95 < 100, $"p95 {p95:F2}ms exceeds the 100ms simulation budget");
    }
}
