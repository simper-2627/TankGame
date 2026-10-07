using GameLogic.Versions;

namespace GameTest;

public class VersionCatalogTests : IDisposable
{
    readonly string dir = Directory.CreateTempSubdirectory("versions").FullName;

    public void Dispose() => Directory.Delete(dir, true);

    void Write(string name, string json) => File.WriteAllText(Path.Combine(dir, name), json);

    static string Entry(string id, string url = "https://x.ts.net", string updatedAt = "2026-10-01T00:00:00Z") =>
        $$"""{"id":"{{id}}","name":"{{id}}-branch","url":"{{url}}","sha":"abc","updatedAt":"{{updatedAt}}"}""";

    [Fact]
    public void MissingDirectoryStillListsTheCurrentEnvironment()
    {
        var list = VersionCatalog.Load(Path.Combine(dir, "nope"), "tankgame");

        var only = Assert.Single(list.Versions);
        Assert.True(only.Current);
        Assert.Equal("Production", only.Name);
    }

    [Fact]
    public void ProductionIsFirstFollowedByNewestPreview()
    {
        Write("tankgame-old.json", Entry("tankgame-old", updatedAt: "2026-10-01T00:00:00Z"));
        Write("tankgame-new.json", Entry("tankgame-new", updatedAt: "2026-10-05T00:00:00Z"));

        var list = VersionCatalog.Load(dir, "tankgame");

        Assert.Equal(["tankgame", "tankgame-new", "tankgame-old"], list.Versions.Select(v => v.Id));
        Assert.Equal("tankgame", list.Current);
    }

    [Fact]
    public void RunningPreviewIsMarkedCurrent()
    {
        Write("tankgame-feat.json", Entry("tankgame-feat"));

        var list = VersionCatalog.Load(dir, "tankgame-feat");

        Assert.Equal("tankgame-feat", Assert.Single(list.Versions, v => v.Current).Id);
    }

    [Fact]
    public void BadFilesAndNonWebUrlsAreSkipped()
    {
        Write("broken.json", "{not json");
        Write("evil.json", Entry("tankgame-evil", url: "javascript:alert(1)"));
        Write("blank.json", Entry(""));
        Write("ignored.txt", Entry("tankgame-txt"));

        var list = VersionCatalog.Load(dir, "tankgame");

        Assert.Equal(["tankgame"], list.Versions.Select(v => v.Id));
    }
}
