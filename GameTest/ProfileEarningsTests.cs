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
