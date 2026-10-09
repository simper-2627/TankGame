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
