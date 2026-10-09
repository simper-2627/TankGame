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
