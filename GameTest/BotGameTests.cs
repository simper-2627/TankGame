using Microsoft.AspNetCore.SignalR;
using GameLogic;
using GameLogic.Game;

namespace GameTest;

public class BotGameTests
{
    private static (Game Game, Guid Human, Guid Bot) BotDuel(BotDifficulty difficulty = BotDifficulty.Hard, int bots = 1,
        FakeClock? clock = null, Random? random = null)
    {
        var game = TestGames.NewGame(
            new MatchSettings { BotCount = bots, BotDifficulty = difficulty, Health = 5 }, clock, GameMatchTypes.Bots, random);
        var human = game.JoinGame();
        return (game, human, game.Tanks.First(t => t.IsBot).Id);
    }

    [Fact]
    public void ABotOnlySeesWhatAHumanWould()
    {
        var (game, human, bot) = BotDuel();

        var view = game.GetGameState(viewerId: bot);
        var seenHuman = view.Tanks!.Single(t => t.Id == human);
        var seenSelf = view.Tanks!.Single(t => t.Id == bot);

        Assert.Null(seenHuman.Health);
        Assert.Null(seenHuman.Deaths);
        Assert.Null(seenHuman.ReloadMsLeft);
        Assert.Equal(5, seenSelf.Health);
        Assert.NotNull(seenSelf.ReloadMsLeft);
    }

    [Fact]
    public async Task ABotWithAClearShotFiresOnTheFirstTick()
    {
        var (game, _, bot) = BotDuel();

        await game.loopRunner.ProcessGameTick();

        Assert.Contains(game.Bullets, bullet => bullet.OwnerId == bot);
    }

    [Fact]
    public async Task ABotDrivesTowardsAHumanThatIsFarAway()
    {
        var (game, human, bot) = BotDuel();
        game.Tanks = game.Tanks.Select(t => t.Id == bot ? t with { PositionX = 700, PositionY = 60 } : t).ToArray();

        await game.loopRunner.ProcessGameTick();

        Assert.True(game.Tanks.Single(t => t.Id == bot).PositionX < 700);
    }

    [Fact]
    public async Task ABotNeverFiresFasterThanTheReload()
    {
        var clock = new FakeClock();
        var (game, _, bot) = BotDuel(clock: clock);
        var shotsSeen = new HashSet<Guid>();

        // Reload is 1000 ms and the clock only moves when the test says so, so only the first shot can leave.
        // Bullets are counted by id as they appear, because the first one hits the human and disappears
        for (var tick = 0; tick < 20; tick++)
        {
            await game.loopRunner.ProcessGameTick();
            foreach (var bullet in game.Bullets.Where(bullet => bullet.OwnerId == bot))
                shotsSeen.Add(bullet.Id);
        }

        Assert.Single(shotsSeen);
    }

    [Fact]
    public async Task EveryBotInTheMatchGetsItsInputEachTick()
    {
        var (game, _, _) = BotDuel(bots: 2);

        await game.loopRunner.ProcessGameTick();

        Assert.Equal(2, game.Bullets.Select(bullet => bullet.OwnerId).Distinct().Count());
    }

    [Fact]
    public void TheCreatorCanChangeTheDifficultyButNotTheBotCount()
    {
        var (game, human, _) = BotDuel(BotDifficulty.Easy, bots: 2);

        game.UpdateMatchSettings(human, game.Settings with { BotDifficulty = BotDifficulty.Hard, BotCount = 5 });

        Assert.Equal(BotDifficulty.Hard, game.Settings.BotDifficulty);
        Assert.Equal(2, game.Settings.BotCount);
    }

    [Fact]
    public void DeveloperSimulationCanAddABotOnDemand()
    {
        var game = TestGames.NewGame(matchType: GameMatchTypes.DeveloperSimulation);
        var human = game.JoinGame();

        var botId = game.AddBot();

        Assert.NotNull(botId);
        Assert.True(game.Tanks.Single(t => t.Id == botId).IsBot);
        Assert.Equal(human, game.CreatorId);
        Assert.NotNull(game.StartedAtTick);
    }

    [Fact]
    public async Task DeveloperSimulationWithOneHumanAndABotKeepsPlaying()
    {
        var game = TestGames.NewGame(matchType: GameMatchTypes.DeveloperSimulation);
        game.JoinGame();
        game.AddBot();

        for (var tick = 0; tick < 5; tick++)
            await game.loopRunner.ProcessGameTick();

        Assert.Equal(GameStatus.Playing, game.Status);
    }

    [Fact]
    public void OtherMatchTypesCannotAddABotByHand()
    {
        var game = TestGames.NewGame();
        game.JoinGame();

        Assert.Null(game.AddBot());
        Assert.Single(game.Tanks);
    }

    [Fact]
    public async Task DeveloperSimulationShowsTheBotsState()
    {
        var game = TestGames.NewGame(new MatchSettings { BotDifficulty = BotDifficulty.Hard },
            matchType: GameMatchTypes.DeveloperSimulation);
        game.JoinGame();
        var botId = game.AddBot();

        await game.loopRunner.ProcessGameTick();

        Assert.Equal(GameStatus.Playing, game.Status);
        var label = game.GetGameState().Tanks!.Single(t => t.Id == botId).BotState;
        Assert.Equal("ATTACK", label);
    }

    [Fact]
    public async Task OutsideDeveloperSimulationTheBotsStateStaysPrivate()
    {
        var (game, _, bot) = BotDuel();

        await game.loopRunner.ProcessGameTick();

        Assert.Null(game.GetGameState().Tanks!.Single(t => t.Id == bot).BotState);
    }

    [Fact]
    public async Task TheHubAddsABotToADeveloperSimulation()
    {
        var lobby = new Lobby(new FakeHubContext());
        lobby.CreateGame("dev", null, GameMatchTypes.DeveloperSimulation).JoinGame();
        var hub = new LobbyHub(lobby);

        await hub.AddBot("dev");

        Assert.Equal(2, lobby.Games.Single().Tanks.Count());
    }

    [Fact]
    public async Task ABotWhoseBrainThrowsDoesNotStopTheTickOrTheOtherBots()
    {
        var random = new ThrowsOnce();
        var (game, _, _) = BotDuel(bots: 2, random: random);
        random.Armed = true;

        await game.loopRunner.ProcessGameTick();

        Assert.Single(game.Bullets.Where(bullet => game.Tanks.Single(t => t.Id == bullet.OwnerId).IsBot)
            .Select(bullet => bullet.OwnerId).Distinct());
    }

    [Fact]
    public async Task TheHubRefusesToAddABotOutsideDeveloperSimulation()
    {
        var lobby = new Lobby(new FakeHubContext());
        lobby.CreateGame("mp", null, GameMatchTypes.Multiplayer).JoinGame();
        var hub = new LobbyHub(lobby);

        await Assert.ThrowsAsync<HubException>(() => hub.AddBot("mp"));
    }

    // Once armed, fails the first time any bot asks it for a number, then behaves
    private sealed class ThrowsOnce : Random
    {
        public bool Armed { get; set; }
        private bool thrown;
        private void Maybe()
        {
            if (!Armed || thrown) return;
            thrown = true;
            throw new InvalidOperationException("bad bot");
        }
        public override double NextDouble() { Maybe(); return base.NextDouble(); }
        public override int Next(int maxValue) { Maybe(); return base.Next(maxValue); }
        public override int Next(int minValue, int maxValue) { Maybe(); return base.Next(minValue, maxValue); }
    }
}
