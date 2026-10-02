using GameLogic;
using GameLogic.Game;

namespace GameTest;

public class BotJoinTests
{
    private static MatchSettings WithBots(int count) => new() { BotCount = count };

    [Fact]
    public void SinglePlayerBotsJoinRightAfterTheCreator()
    {
        var game = TestGames.NewGame(WithBots(2), matchType: GameMatchTypes.Bots);

        var creator = game.JoinGame();

        Assert.Equal(3, game.Tanks.Count());
        Assert.False(game.Tanks.Single(t => t.Id == creator).IsBot);
        Assert.Equal(2, game.Tanks.Count(t => t.IsBot));
        Assert.Equal(creator, game.CreatorId);
        Assert.NotNull(game.StartedAtTick);
    }

    [Fact]
    public void SinglePlayerAlwaysHasAtLeastOneBot()
    {
        var game = TestGames.NewGame(WithBots(0), matchType: GameMatchTypes.Bots);

        game.JoinGame();

        Assert.Equal(1, game.Tanks.Count(t => t.IsBot));
    }

    [Fact]
    public void NoOtherHumanCanJoinASinglePlayerMatch()
    {
        var game = TestGames.NewGame(WithBots(1), matchType: GameMatchTypes.Bots);
        game.JoinGame();

        var error = Assert.Throws<InvalidOperationException>(() => game.JoinGame());

        Assert.Contains("single player", error.Message);
        Assert.Equal(2, game.Tanks.Count());
    }

    [Fact]
    public void BotCountLeavesRoomForHumans()
    {
        Assert.Equal(7, TestGames.NewGame(WithBots(7), matchType: GameMatchTypes.Bots).BotSlots);
        // Multiplayer needs seats for at least 2 humans
        Assert.Equal(6, TestGames.NewGame(WithBots(7)).BotSlots);
    }

    [Fact]
    public void MultiplayerBotsJoinWithTheSecondHuman()
    {
        var game = TestGames.NewGame(WithBots(2));

        game.JoinGame();
        Assert.Single(game.Tanks);
        Assert.Null(game.StartedAtTick);

        game.JoinGame();
        Assert.Equal(4, game.Tanks.Count());
        Assert.Equal(2, game.Tanks.Count(t => t.IsBot));
        Assert.NotNull(game.StartedAtTick);
    }

    [Fact]
    public void HumansCannotTakeTheSeatsTheBotsNeed()
    {
        var game = TestGames.NewGame(WithBots(6));
        game.JoinGame();
        game.JoinGame();

        Assert.Equal(8, game.Tanks.Count());
        Assert.Throws<InvalidOperationException>(() => game.JoinGame());
        Assert.Equal(8, game.Tanks.Count());
    }

    [Fact]
    public void MultiplayerWithoutBotsIsUnchanged()
    {
        var game = TestGames.NewGame();

        game.JoinGame();
        game.JoinGame();

        Assert.Equal(2, game.Tanks.Count());
        Assert.DoesNotContain(game.Tanks, t => t.IsBot);
    }

    [Fact]
    public void DeveloperSimulationNeverAddsBotsByItself()
    {
        var game = TestGames.NewGame(WithBots(3), matchType: GameMatchTypes.DeveloperSimulation);

        game.JoinGame();
        game.JoinGame();

        Assert.Equal(2, game.Tanks.Count());
        Assert.DoesNotContain(game.Tanks, t => t.IsBot);
    }

    [Fact]
    public void BotsGetDistinctNames()
    {
        var game = TestGames.NewGame(WithBots(5), matchType: GameMatchTypes.Bots);

        game.JoinGame("Ben");

        var names = game.Tanks.Select(t => t.Name).ToArray();
        Assert.All(names, name => Assert.False(string.IsNullOrWhiteSpace(name)));
        Assert.Equal(names.Length, names.Distinct().Count());
    }

    [Fact]
    public void MoreBotsThanSpawnPointsStillJoinAndTheExtraOnesWait()
    {
        // The test arena has 3 spawn points; 1 human + 5 bots is 6 tanks
        var game = TestGames.NewGame(WithBots(5), matchType: GameMatchTypes.Bots);

        game.JoinGame();

        Assert.Equal(6, game.Tanks.Count());
        Assert.Equal(3, game.Tanks.Count(t => t.Respawning));
    }

    [Fact]
    public void ABotMatchOnAMapOtherThanStandardIsRefused()
    {
        var lobby = new Lobby(new FakeHubContext());
        var bigMap = MapCatalog.FixedMaps.First(m => m.Mode == MapMode.BigMap).Name;

        Assert.Throws<InvalidOperationException>(() =>
            lobby.CreateGame("big", bigMap, GameMatchTypes.Bots, new MatchSettings { BotCount = 1 }));

        Assert.Empty(lobby.Games);
    }

    [Fact]
    public void ABotMatchOnAStandardMapIsCreatedAsABotMatch()
    {
        var lobby = new Lobby(new FakeHubContext());
        var standardMap = MapCatalog.FixedMaps.First(m => m.Mode == MapMode.Standard).Name;

        var game = lobby.CreateGame("solo", standardMap, GameMatchTypes.Bots, new MatchSettings { BotCount = 2 });

        Assert.Equal(GameMatchTypes.Bots, game.MatchType);
        Assert.Single(lobby.Games);
    }

    [Theory]
    [InlineData(MapMode.BigMap, 0)]
    [InlineData(MapMode.Foggish, 0)]
    [InlineData(MapMode.Standard, 3)]
    public void MultiplayerKeepsItsBotsOnStandardMapsOnly(MapMode mode, int expectedBots)
    {
        var lobby = new Lobby(new FakeHubContext());
        var map = MapCatalog.FixedMaps.First(m => m.Mode == mode).Name;

        var game = lobby.CreateGame("mp", map, GameMatchTypes.Multiplayer,
            new MatchSettings { BotCount = 3, ClearBotsToWin = true });
        game.JoinGame();
        game.JoinGame();

        Assert.Equal(expectedBots, game.Tanks.Count(t => t.IsBot));
        Assert.Equal(expectedBots > 0, game.Settings.ClearBotsToWin);
    }
}
