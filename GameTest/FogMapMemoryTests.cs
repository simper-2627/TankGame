using GameLogic;
using Xunit;

namespace GameTest;

public class FogMapMemoryTests
{
    private static readonly GameMap Map = new("Recon", 4500, 2800, [], []) { Mode = MapMode.Foggish };
    private static readonly Guid Viewer = Guid.NewGuid();
    private static TankState Enemy(int x, int y) => new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), PositionX = x, PositionY = y, Health = 3 };

    [Fact]
    public void UnseenEnemiesAreHiddenAndDepartingEnemiesStayAtLastSeenPosition()
    {
        var memory = new FogMapMemory();
        var camera = new MapCamera(1, 0, 0);
        var enemy = Enemy(100, 100);
        memory.Observe(Map, camera, 500, 500, [enemy], Viewer, 0);
        var seen = memory.Contacts[enemy.Id];
        memory.Observe(Map, camera, 500, 500, [enemy with { PositionX = 3000 }], Viewer, 0);
        Assert.Equal(seen.X, memory.Contacts[enemy.Id].X);
        Assert.False(memory.Contacts[enemy.Id].Visible);
        var fresh = new FogMapMemory();
        fresh.Observe(Map, camera, 500, 500, [enemy with { PositionX = 3000 }], Viewer, 0);
        Assert.Empty(fresh.Contacts);
    }

    [Fact]
    public void ExplorationPersistsAndRevisitingEmptyLocationClearsOldContact()
    {
        var memory = new FogMapMemory();
        var enemy = Enemy(100, 100);
        memory.Observe(Map, new(1, 0, 0), 400, 400, [enemy], Viewer, 0);
        var initial = memory.ExploredPath;
        memory.Observe(Map, new(1, 2000, 1000), 400, 400, [enemy], Viewer, 0);
        Assert.Contains(initial.Trim(), memory.ExploredPath);
        Assert.False(memory.Contacts[enemy.Id].Visible);
        memory.Observe(Map, new(1, 0, 0), 400, 400, [enemy with { PositionX = 3500 }], Viewer, 0);
        Assert.Empty(memory.Contacts);
        Assert.NotEmpty(memory.ExploredPath);
    }

    [Fact]
    public void OwnTankAndRespawningTanksAreNotEnemyContacts()
    {
        var memory = new FogMapMemory();
        var enemy = Enemy(100, 100);
        memory.Observe(Map, new(.5, 0, 0), 500, 500,
            [enemy with { Health = 0 }, enemy with { Id = Viewer }], Viewer, 26);
        Assert.Empty(memory.Contacts);
    }
}
