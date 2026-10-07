namespace GameLogic;

public record Tank
{
    public long InputSequence { get; init; }
    public const int Size = 60;
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    // Computer-controlled; public to everyone so the screen can draw bots in red
    public bool IsBot { get; init; }
    public int PositionY { get; init; } = 50;
    public int PositionX { get; init; } = 50;
    public int Angle { get; init; } = -45;
    public int Speed { get; init; } = 0;
    // Driving backwards: the hull faces away from the direction of travel
    public bool Reversing { get; init; }
    public bool MovingUp { get; init; }
    public bool MovingLeft { get; init; }
    public bool MovingRight { get; init; }
    public bool Shooting { get; init; }
    public bool BoostHeld { get; init; }
    public bool Boosting { get; init; }
    public bool BoostLocked { get; init; }
    public double BoostEnergy { get; init; } = 100;
    public const double BoostMaxEnergy = 100;
    public bool MovingDown { get; init; }
    // Point the turret aims at (the player's mouse), in board coordinates
    public int? AimX { get; init; }
    public int? AimY { get; init; }
    public int TurretAngle { get; init; } = -45;
    // Hits left before elimination
    public int Health { get; init; } = MatchSettings.DefaultHealth;
    // Out for good: used up every life
    public bool Eliminated { get; init; }
    // Times this tank has been destroyed
    public int Deaths { get; init; }
    // Ticks until a destroyed tank comes back
    public int RespawnTicksLeft { get; init; }
    // Where a waiting tank will come back. Only its owner is told (see Game.GetGameState)
    public MapSpawnPoint? PendingSpawn { get; init; }
    // Ticks left of the red "just got hit" flash; public to everyone, unlike the health that caused it
    public const int HitFlashTicksOnHit = 1;
    public int HitFlashTicks { get; init; }
    // Destroyed but with lives left: waiting to respawn, can't move, shoot or be hit
    public bool Respawning => Health <= 0 && !Eliminated;
    // Ticks of post-respawn invincibility; everyone can see Shielded status, but not duration
    public const int ShieldSeconds = 3;
    public const int ShieldDurationTicks = ShieldSeconds * Game.GameLoopRunner.TicksPerSecond;
    public int ShieldTicksLeft { get; init; }
    public bool Shielded => ShieldTicksLeft > 0;
    // Hits on other tanks; breaks health ties when time runs out
    public int HitsLanded { get; init; }
    // Game clock time (ms) at which the tank may fire again; 0 = ready
    public long NextShotAtMs { get; init; }
    //public Bullet Bullet { get; set; } = new();

    // Distance from the turret pivot to the muzzle, matching the drawn barrel
    public const int BarrelLength = 40;

    public static Tank ProcessTankMovement(Tank tank)
    {
        return ProcessTankMovement(tank, MapCatalog.DefaultMap, new DeveloperGameSettings());
    }

    public static Tank ProcessTankMovement(Tank tank, GameMap map)
    {
        return ProcessTankMovement(tank, map, new DeveloperGameSettings());
    }

    public static Tank ProcessTankMovement(Tank tank, GameMap map, DeveloperGameSettings settings)
    {
        if (tank.Eliminated || tank.Respawning)
            return tank;

        var boosted = ApplyBoost(tank, settings);
        var turnedShip = CalculateNewAngleAndSpeed(boosted, settings);
        var movedShip = CalculateNewPosition(turnedShip, map, settings);
        //CalculateShooting(movedShip);
        return AimTurret(movedShip, settings);
    }

    // Center of the drawn tank, which the turret rotates around
    public static (int X, int Y) GetCenter(Tank tank, DeveloperGameSettings settings) =>
        (tank.PositionX + Size / 2, tank.PositionY - settings.VisualTopOffset + Size / 2);

    // Re-aim every tick so the turret stays on the cursor while the tank drives
    public static Tank AimTurret(Tank tank, DeveloperGameSettings settings)
    {
        if (tank.AimX is null || tank.AimY is null)
            return tank;

        var (centerX, centerY) = GetCenter(tank, settings);
        var deltaX = tank.AimX.Value - centerX;
        var deltaY = tank.AimY.Value - centerY;
        if (deltaX == 0 && deltaY == 0)
            return tank;

        var newAngle = (int)Math.Round(Math.Atan2(deltaY, deltaX) * 180.0 / Math.PI);
        return tank with { TurretAngle = newAngle };
    }

    // Bullet leaving the muzzle along the turret's direction
    public static Bullet FireBullet(Tank tank, DeveloperGameSettings settings, int bounces = 0, int speed = Bullet.DefaultSpeed)
    {
        var (centerX, centerY) = GetCenter(tank, settings);
        double radians = Math.PI * tank.TurretAngle / 180.0;
        var muzzleX = centerX + (int)Math.Round(BarrelLength * Math.Cos(radians));
        var muzzleY = centerY + (int)Math.Round(BarrelLength * Math.Sin(radians));
        return new Bullet
        {
            // Bullet position is its top-left corner; center it on the muzzle
            PositionX = muzzleX - Bullet.BulletSize / 2,
            PositionY = muzzleY - Bullet.BulletSize / 2,
            Angle = tank.TurretAngle,
            OwnerId = tank.Id,
            BouncesLeft = bounces,
            Speed = speed
        };
    }

    public static RectangleArea GetCollisionArea(Tank tank) =>
        GetCollisionArea(tank, new DeveloperGameSettings());

    public static RectangleArea GetCollisionArea(Tank tank, DeveloperGameSettings settings)
    {
        var hitboxInset = Math.Clamp(settings.HitboxInset, 0, Size / 2 - 1);
        var hitboxSize = Size - (hitboxInset * 2);
        return new(
            tank.PositionX + hitboxInset,
            tank.PositionY - settings.VisualTopOffset + hitboxInset,
            hitboxSize,
            hitboxSize);
    }

    private static Tank CalculateNewAngleAndSpeed(Tank tank, DeveloperGameSettings settings)
    {
        var effectiveMaxSpeed = tank.Boosting ? (int)Math.Round(settings.MaxSpeed * settings.BoostSpeedMultiplier) : settings.MaxSpeed;
        var netX = (tank.MovingRight ? 1 : 0) - (tank.MovingLeft ? 1 : 0);
        var netY = (tank.MovingDown ? 1 : 0) - (tank.MovingUp ? 1 : 0);

        if (netX == 0 && netY == 0)
        {
            // No input: brake (BrakeAcceleration is negative), coasting on any leftover speed
            return tank with { Speed = Math.Clamp(tank.Speed + settings.BrakeAcceleration, 0, effectiveMaxSpeed) };
        }

        var desiredAngle = (int)Math.Round(Math.Atan2(netY, netX) * 180.0 / Math.PI);

        // A slow-turning hull that would have to swing past 90 degrees backs up instead
        var reversing = settings.TurnDegrees < 180
            && Math.Abs(AngleDifference(desiredAngle, tank.Angle)) > 90;
        var targetAngle = reversing ? NormalizeAngle(desiredAngle + 180) : desiredAngle;

        var turn = Math.Clamp(AngleDifference(targetAngle, tank.Angle), -settings.TurnDegrees, settings.TurnDegrees);

        return tank with
        {
            Angle = NormalizeAngle(tank.Angle + turn),
            Reversing = reversing,
            Speed = Math.Clamp(tank.Speed + settings.ForwardAcceleration, 0, effectiveMaxSpeed),
        };
    }

    // Wraps to (-180, 180]
    private static int NormalizeAngle(int angle)
    {
        var wrapped = ((angle % 360) + 360) % 360;
        return wrapped > 180 ? wrapped - 360 : wrapped;
    }

    // Shortest signed turn from `from` to `to`, in (-180, 180]
    private static int AngleDifference(int to, int from) => NormalizeAngle(to - from);

    private static Tank CalculateNewPosition(Tank incomingTank, GameMap map, DeveloperGameSettings settings)
    {
        double radians = Math.PI * incomingTank.Angle / 180.0;
        var speed = incomingTank.Reversing
            ? incomingTank.Speed * settings.BackwardSpeedMultiplier
            : incomingTank.Speed;
        var direction = incomingTank.Reversing ? -1 : 1;
        var deltaX = direction * (int)(speed * Math.Cos(radians));
        var deltaY = direction * (int)(speed * Math.Sin(radians));

        return MoveUntilBlocked(incomingTank, deltaX, deltaY, map, settings);
    }

    private static Tank MoveUntilBlocked(Tank tank, int deltaX, int deltaY, GameMap map, DeveloperGameSettings settings)
    {
        var targetX = Math.Clamp(tank.PositionX + deltaX, 0, map.Width - Size);
        var targetY = Math.Clamp(tank.PositionY + deltaY, 0, map.Height - Size);
        var totalX = targetX - tank.PositionX;
        var totalY = targetY - tank.PositionY;
        // Sliding uses pixel steps so an axis fallback cannot skip a thin obstacle.
        var stepPixels = settings.SlideAlongWalls ? 1 : Math.Max(1, settings.CollisionStepPixels);
        var steps = (int)Math.Ceiling(Math.Max(Math.Abs(totalX), Math.Abs(totalY)) / (double)stepPixels);

        if (steps == 0)
        {
            return tank;
        }

        var lastValidTank = tank;
        for (var step = 1; step <= steps; step++)
        {
            var stepX = (int)Math.Round(totalX * step / (double)steps)
                - (int)Math.Round(totalX * (step - 1) / (double)steps);
            var stepY = (int)Math.Round(totalY * step / (double)steps)
                - (int)Math.Round(totalY * (step - 1) / (double)steps);
            var nextTank = lastValidTank with
            {
                PositionX = lastValidTank.PositionX + stepX,
                PositionY = lastValidTank.PositionY + stepY
            };

            if (map.Blocks(GetCollisionArea(nextTank, settings)))
            {
                if (settings.SlideAlongWalls)
                {
                    var alongX = lastValidTank with { PositionX = nextTank.PositionX };
                    var alongY = lastValidTank with { PositionY = nextTank.PositionY };
                    var canSlideX = stepX != 0 && !map.Blocks(GetCollisionArea(alongX, settings));
                    var canSlideY = stepY != 0 && !map.Blocks(GetCollisionArea(alongY, settings));
                    // At an exact corner neither face is preferred: don't steer the tank sideways.
                    if (canSlideX && canSlideY)
                        return lastValidTank with { Speed = 0 };
                    if (canSlideX)
                    {
                        lastValidTank = alongX;
                        continue;
                    }
                    if (canSlideY)
                    {
                        lastValidTank = alongY;
                        continue;
                    }
                    // A shallow diagonal may have no tangential pixel in this step.
                    continue;
                }
                return lastValidTank with { Speed = 0 };
            }

            lastValidTank = nextTank;
        }

        return lastValidTank.PositionX == tank.PositionX && lastValidTank.PositionY == tank.PositionY
            ? lastValidTank with { Speed = 0 }
            : lastValidTank;
    }

    private static Tank ApplyBoost(Tank tank, DeveloperGameSettings settings)
{
    var hasMovementInput = tank.MovingUp || tank.MovingDown || tank.MovingLeft || tank.MovingRight;
    var wantsBoost = tank.BoostHeld && hasMovementInput && !tank.BoostLocked && tank.BoostEnergy > 0;

    var energy = tank.BoostEnergy;
    var locked = tank.BoostLocked;

    if (wantsBoost)
    {
        energy = Math.Max(0, energy - settings.BoostDrainPerTick);
        if (energy == 0)
            locked = true;
    }
    else
    {
        energy = Math.Min(BoostMaxEnergy, energy + settings.BoostRegenPerTick);
    }

    if (!tank.BoostHeld)
        locked = false;

    return tank with { Boosting = wantsBoost, BoostEnergy = energy, BoostLocked = locked };
}

    public static RectangleArea GetVisualArea(Tank tank) =>
        new(
            tank.PositionX,
            tank.PositionY - new DeveloperGameSettings().VisualTopOffset,
            Size,
            Size);

    //private static Bullet CalculateShooting(Tank incomingTank)
    //{
    //    Bullet bullet = new();
    //    if (incomingTank.Shooting)
    //    {
    //        bullet = new Bullet
    //        {
    //            PositionX = incomingTank.PositionX,
    //            PositionY = incomingTank.PositionY,
    //            Angle = incomingTank.Angle
    //        };
            
    //    }
    //    var updatedBullet = Bullet.MoveBullet(bullet);

    //    return updatedBullet;
    //}
}
