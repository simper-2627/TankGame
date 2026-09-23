using GameLogic;
using GameLogic.Game;
using Microsoft.AspNetCore.SignalR;

namespace GameTest;

public class UnitTest1
{
    private class TestHubClients : IHubClients
    {
        public IClientProxy this[string connectionId] => new TestClientProxy();

        public IClientProxy All => new TestClientProxy();

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => new TestClientProxy();

        public IClientProxy Client(string connectionId) => new TestClientProxy();

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new TestClientProxy();

        public IClientProxy Group(string groupName) => new TestClientProxy();

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => new TestClientProxy();

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => new TestClientProxy();

        public IClientProxy User(string userId) => new TestClientProxy();

        public IClientProxy Users(IReadOnlyList<string> userIds) => new TestClientProxy();
    }

    private class TestClientProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private class TestGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
    private class TestHubContext : IHubContext<LobbyHub>
    {
        public IHubClients Clients { get; } = new TestHubClients();
        public IGroupManager Groups { get; } = new TestGroupManager();
    }

    [Fact]
    public void BulletMotion()
    {
        
        var hubContext = new TestHubContext();
        Game game = new(hubContext);
        var playerId = game.JoinGame();

        var playerInput = new PlayerInputRequest
        {
            GameName = "TestGame",
            PlayerId = playerId,
            Up = false,
            Left = false,
            Right = false,
            Down = false,
            Shoot = true,
        };

        game.ReceiveUserInput(playerInput);

        var gameState = game.GetGameState();
        var bullet = gameState.Bullets.FirstOrDefault();
        Assert.NotNull(bullet);
        Assert.Equal(game.Tanks.First().PositionX, bullet.PositionX);
        Assert.Equal(game.Tanks.First().PositionY, bullet.PositionY);

        game.loopRunner.ProcessGameTick().Wait();

        var updatedGameState = game.GetGameState();
        var updatedBullet = updatedGameState.Bullets.FirstOrDefault();
        Assert.NotNull(updatedBullet);
        Assert.NotEqual(bullet.PositionX, updatedBullet.PositionX);
        Assert.NotEqual(bullet.PositionY, updatedBullet.PositionY);
    }
}
