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
