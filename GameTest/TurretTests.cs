using GameLogic;

namespace GameTest;

public class TurretTests
{
    private static readonly DeveloperGameSettings settings = new();

    // Position is the drawn box's corner; place the tank so its center lands on (x, y)
    private static Tank TankCenteredAt(int x, int y) => new()
    {
        PositionX = x - Tank.Size / 2,
        PositionY = y + settings.VisualTopOffset - Tank.Size / 2,
    };

    [Fact]
    public void CenterMatchesDrawnBox()
    {
        Assert.Equal((100, 100), Tank.GetCenter(TankCenteredAt(100, 100), settings));
    }

    [Theory]
    [InlineData(200, 100, 0)]    // right
    [InlineData(100, 200, 90)]   // down (screen y grows downward)
    [InlineData(0, 100, 180)]    // left
    [InlineData(100, 0, -90)]    // up
    [InlineData(200, 200, 45)]   // down-right
    public void TurretPointsAtAim(int aimX, int aimY, int expectedAngle)
    {
        var tank = TankCenteredAt(100, 100) with { AimX = aimX, AimY = aimY };

        Assert.Equal(expectedAngle, Tank.AimTurret(tank, settings).TurretAngle);
    }

    [Fact]
    public void TurretKeepsAngleWithoutAim()
    {
        var tank = new Tank { TurretAngle = 30 };

        Assert.Equal(30, Tank.AimTurret(tank, settings).TurretAngle);
    }

    [Fact]
    public void TurretTracksAimWhileTankMoves()
    {
        // Aim straight right of the tank, then drive down: the turret should swing toward the aim point
        var tank = TankCenteredAt(100, 100) with { AimX = 300, AimY = 100, MovingDown = true };

        var moved = Tank.ProcessTankMovement(tank);

        Assert.Equal(90, moved.Angle);
        Assert.True(moved.TurretAngle < 0, $"expected turret to angle up toward aim, got {moved.TurretAngle}");
    }

    [Fact]
    public void TurretIndependentOfHull()
    {
        var tank = TankCenteredAt(100, 100) with { AimX = 100, AimY = 0, MovingRight = true };

        var moved = Tank.ProcessTankMovement(tank);

        Assert.Equal(0, moved.Angle);
        Assert.NotEqual(moved.Angle, moved.TurretAngle);
    }
}
