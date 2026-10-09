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
