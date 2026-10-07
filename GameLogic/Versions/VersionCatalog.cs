using System.Text.Json;

namespace GameLogic.Versions;

public record VersionEntry(string Id, string Name, string Url, string Sha, DateTimeOffset? UpdatedAt, bool Current);

public record VersionList(string Current, IReadOnlyList<VersionEntry> Versions);

// CI keeps one JSON file per live preview environment in a directory (a mounted ConfigMap in k8s).
// This reads them so the running app can offer a switcher without a redeploy when previews come and go.
public static class VersionCatalog
{
    public const string ProductionNamespace = "tankgame";
    const string NamespacePrefix = "tankgame-";

    public static VersionList Load(string directory, string? currentNamespace)
    {
        var current = string.IsNullOrWhiteSpace(currentNamespace) ? ProductionNamespace : currentNamespace;
        var found = ReadEntries(directory, current);

        // The environment we're running in is always listed, even if CI hasn't registered it (prod never is).
        if (!found.Any(v => v.Current))
            found.Add(new VersionEntry(current, DisplayName(current), "", "", null, true));

        var ordered = found
            .OrderByDescending(v => v.Current)
            .ThenByDescending(v => v.UpdatedAt ?? DateTimeOffset.MinValue)
            .ToList();
        return new VersionList(current, ordered);
    }

    static List<VersionEntry> ReadEntries(string directory, string current)
    {
        var entries = new List<VersionEntry>();
        if (!Directory.Exists(directory)) return entries;

        foreach (var file in Directory.GetFiles(directory, "*.json"))
        {
            var entry = TryParse(file, current);
            if (entry != null) entries.Add(entry);
        }
        return entries;
    }

    static VersionEntry? TryParse(string file, string current)
    {
        try
        {
            var raw = JsonSerializer.Deserialize<VersionEntry>(File.ReadAllText(file), JsonSerializerOptions.Web);
            if (raw == null || string.IsNullOrWhiteSpace(raw.Id)) return null;
            // Only ever navigate to web links, whatever ends up in the file.
            if (!Uri.TryCreate(raw.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")) return null;
            return raw with
            {
                Name = string.IsNullOrWhiteSpace(raw.Name) ? DisplayName(raw.Id) : raw.Name,
                Sha = raw.Sha ?? "",
                Current = raw.Id == current,
            };
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    static string DisplayName(string ns)
    {
        if (ns == ProductionNamespace) return "Production";
        return ns.StartsWith(NamespacePrefix) ? ns[NamespacePrefix.Length..] : ns;
    }
}
