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
