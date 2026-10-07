using GameLogic;

namespace GameTest;

public class PlayerNameTests
{
    [Fact]
    public void ChosenNameIsKeptAndSentToEveryViewer()
    {
        var game = TestGames.NewGame();
        var me = game.JoinGame("  Marc ");
        var other = game.JoinGame();

        var seenByOther = game.GetGameState(viewerId: other).Tanks!.Single(t => t.Id == me);
        Assert.Equal("Marc", seenByOther.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingNameGetsAGeneratedOne(string? name)
    {
        var game = TestGames.NewGame();
        var id = game.JoinGame(name);

        Assert.False(string.IsNullOrWhiteSpace(game.GetGameState().Tanks!.Single(t => t.Id == id).Name));
    }

    [Fact]
    public void GeneratedNamesAreUniqueWithinAGame()
    {
        var game = TestGames.NewGame();
        for (var i = 0; i < 8; i++) game.JoinGame();

        var names = game.GetGameState().Tanks!.Select(t => t.Name).ToArray();
        Assert.Equal(names.Length, names.Distinct().Count());
    }
}
