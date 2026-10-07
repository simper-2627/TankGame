namespace GameLogic;

// WinnerId is null when an ended match is a draw, or when the bots won (BotsWon); team matches set WinningTeam instead
public record MatchResult(bool Ended, Guid? WinnerId, bool BotsWon = false, int? WinningTeam = null)
{
    public static readonly MatchResult Ongoing = new(false, null);
}

public static class Combat
{
    // Each bullet hits at most one tank (the first it overlaps) and is used up;
    // eliminated tanks can't be hit, so later bullets fly on
    public static (Tank[] Tanks, Bullet[] Bullets) ResolveHits(
        IEnumerable<Tank> tanks, IEnumerable<Bullet> bullets, DeveloperGameSettings settings, MatchSettings? match = null)
    {
        match ??= new MatchSettings();
        var tankList = tanks.ToList();
        var flying = new List<Bullet>();

        foreach (var bullet in bullets)
        {
            var bulletArea = new RectangleArea(bullet.PositionX, bullet.PositionY, Bullet.BulletSize, Bullet.BulletSize);
            var targetIndex = tankList.FindIndex(tank =>
                !tank.Eliminated && !tank.Respawning && Tank.GetCollisionArea(tank, settings).Intersects(bulletArea));
            if (targetIndex < 0)
            {
                flying.Add(bullet);
                continue;
            }

            ApplyHit(tankList, targetIndex, bullet.OwnerId, match);
        }

        return (tankList.ToArray(), flying.ToArray());
    }

    // One hit: 1 health off the target, credited to the shooter
    public static void ApplyHit(List<Tank> tankList, int targetIndex, Guid shooterId, MatchSettings match)
    {
        var target = tankList[targetIndex];
        if (target.Shielded)
            return;

        var health = Math.Max(0, target.Health - 1);
        tankList[targetIndex] = health > 0 ? target with { Health = health, HitFlashTicks = Tank.HitFlashTicksOnHit } : Destroy(target, match);

        // Shooting yourself with a bounce doesn't count as a hit landed
        var shooterIndex = tankList.FindIndex(tank => tank.Id == shooterId);
        if (shooterIndex >= 0 && shooterId != target.Id)
            tankList[shooterIndex] = tankList[shooterIndex] with { HitsLanded = tankList[shooterIndex].HitsLanded + 1 };
    }

    // Instant shot: walk from the muzzle along the turret until something solid is met.
    // Steps are smaller than any tank or wall, so nothing can be skipped
    public static InstantShot TraceShot(Tank shooter, IReadOnlyList<Tank> tanks, GameMap map, DeveloperGameSettings settings)
    {
        const int step = 4;
        var (centerX, centerY) = Tank.GetCenter(shooter, settings);
        var radians = Math.PI * shooter.TurretAngle / 180.0;
        var range = (int)Math.Ceiling(Math.Sqrt((double)map.Width * map.Width + (double)map.Height * map.Height));
        var (x, y) = (centerX, centerY);

        for (var distance = Tank.BarrelLength; distance <= range; distance += step)
        {
            x = centerX + (int)Math.Round(distance * Math.Cos(radians));
            y = centerY + (int)Math.Round(distance * Math.Sin(radians));
            if (map.BlocksPoint(x, y))
                return new InstantShot(x, y, null);

            var hit = -1;
            for (var i = 0; i < tanks.Count; i++)
            {
                var tank = tanks[i];
                if (tank.Id == shooter.Id || tank.Eliminated || tank.Respawning)
                    continue;
                var area = Tank.GetCollisionArea(tank, settings);
                if (x >= area.X && x <= area.X + area.Width && y >= area.Y && y <= area.Y + area.Height)
                {
                    hit = i;
                    break;
                }
            }
            if (hit >= 0)
                return new InstantShot(x, y, hit);
        }

        return new InstantShot(x, y, null);
    }

    // Health hit 0: that's a death. The last life is permanent; otherwise the tank waits to respawn
    private static Tank Destroy(Tank tank, MatchSettings match)
    {
        var deaths = tank.Deaths + 1;
        var outForGood = deaths >= match.Lives;
        return tank with
        {
            Health = 0,
            Deaths = deaths,
            Eliminated = outForGood,
            RespawnTicksLeft = outForGood ? 0 : match.RespawnSeconds * Game.GameLoopRunner.TicksPerSecond,
            Speed = 0,
            MovingUp = false, MovingDown = false, MovingLeft = false, MovingRight = false,
            Shooting = false,
        };
    }

    // Update the working list immediately so simultaneous respawns cannot claim the same space.
    public static Tank[] TickRespawns(IEnumerable<Tank> tanks, GameMap map, MatchSettings match, Random rng, DeveloperGameSettings? settings = null)
    {
        var result = tanks.ToArray();
        for (var i = 0; i < result.Length; i++)
        {
            var tank = result[i];
            if (!tank.Respawning) continue;
            // Picked ahead of time so the owner can see where they'll return; re-picked only if someone takes the spot
            var spawn = tank.PendingSpawn is { } pending && SpawnSelector.IsFree(map, result, pending, settings)
                ? pending
                : SpawnSelector.Choose(map, result, rng, settings);
            if (tank.RespawnTicksLeft > 1)
            {
                result[i] = tank with { RespawnTicksLeft = tank.RespawnTicksLeft - 1, PendingSpawn = spawn };
                continue;
            }
            result[i] = spawn is null ? tank with { RespawnTicksLeft = 0, PendingSpawn = null } : tank with
            {
                PositionX = spawn.X, PositionY = spawn.Y,
                Angle = spawn.Angle, TurretAngle = spawn.Angle,
                Health = match.Health, RespawnTicksLeft = 0, NextShotAtMs = 0,
                AimX = null, AimY = null, PendingSpawn = null,
                ShieldTicksLeft = Tank.ShieldDurationTicks
            };
        }
        return result;
    }

    // A match needs 2 players before it can end, or the creator would win alone.
    // ticksLeft is null when there's no time limit (or it hasn't started).
    // With bots in the match the rules change (see DecideWithBots); without them it's last tank standing
    public static MatchResult DecideResult(IReadOnlyCollection<Tank> tanks, int? ticksLeft,
        bool singlePlayer = false, bool clearBotsToWin = false, GameMode mode = GameMode.FreeForAll)
    {
        if (mode == GameMode.TeamElimination)
            return DecideTeamResult(tanks, ticksLeft);

        if (tanks.Count < 2)
            return MatchResult.Ongoing;
        if (tanks.Any(tank => tank.IsBot))
            return DecideWithBots(tanks, ticksLeft, singlePlayer, clearBotsToWin);

        var alive = tanks.Where(tank => !tank.Eliminated).ToList();
        if (alive.Count == 0)
            return new MatchResult(true, null);
        if (alive.Count == 1)
            return new MatchResult(true, alive[0].Id);
        if (ticksLeft is <= 0)
            return ByHealthThenHits(alive);
        return MatchResult.Ongoing;
    }

    // Single player: clear every bot before the clock runs out. Multiplayer: bots are a hazard, not the opponent,
    // so the last human standing wins (unless the creator asked for the bots to be cleared too) and only humans are ranked on time
    private static MatchResult DecideWithBots(IReadOnlyCollection<Tank> tanks, int? ticksLeft, bool singlePlayer, bool clearBotsToWin)
    {
        var humansAlive = tanks.Where(tank => !tank.IsBot && !tank.Eliminated).ToList();
        var botsAlive = tanks.Count(tank => tank.IsBot && !tank.Eliminated);
        var timeUp = ticksLeft is <= 0;

        // A multiplayer match can't end until a second human has joined (the Developer simulation can add a bot early)
        if (!singlePlayer && tanks.Count(tank => !tank.IsBot) < 2)
            return MatchResult.Ongoing;

        if (humansAlive.Count == 0)
            return botsAlive > 0 ? new MatchResult(true, null, BotsWon: true) : new MatchResult(true, null);

        if (singlePlayer)
        {
            if (botsAlive == 0)
                return new MatchResult(true, humansAlive[0].Id);
            return timeUp ? new MatchResult(true, null, BotsWon: true) : MatchResult.Ongoing;
        }

        var mustClearBots = clearBotsToWin && botsAlive > 0;
        if (humansAlive.Count == 1 && (!mustClearBots || timeUp))
            return new MatchResult(true, humansAlive[0].Id);
        if (humansAlive.Count >= 2 && timeUp)
            return ByHealthThenHits(humansAlive);
        return MatchResult.Ongoing;
    }

    // Last team with a tank left wins. Both teams need a player first, or the first to join would win alone.
    // Bots have no team: they are a hazard to everyone, so they never decide a team match
    private static MatchResult DecideTeamResult(IReadOnlyCollection<Tank> tanks, int? ticksLeft)
    {
        var teams = tanks.Where(tank => tank.Team is not null).GroupBy(tank => tank.Team!.Value).ToList();
        if (teams.Count < 2)
            return MatchResult.Ongoing;

        var standing = teams.Where(team => team.Any(tank => !tank.Eliminated)).ToList();
        if (standing.Count == 0)
            return new MatchResult(true, null, BotsWon: tanks.Any(tank => tank.IsBot && !tank.Eliminated));
        if (standing.Count == 1)
            return new MatchResult(true, null, WinningTeam: standing[0].Key);
        if (ticksLeft is <= 0)
            return ByTeamSurvivors(standing);
        return MatchResult.Ongoing;
    }

    // Time ran out: most tanks still in wins, then fewest deaths across the team; a full tie is a draw
    private static MatchResult ByTeamSurvivors(List<IGrouping<int, Tank>> standing)
    {
        var ranked = standing
            .Select(team => (Team: team.Key, Alive: team.Count(tank => !tank.Eliminated), Deaths: team.Sum(tank => tank.Deaths)))
            .OrderByDescending(team => team.Alive).ThenBy(team => team.Deaths)
            .ToList();
        var (first, second) = (ranked[0], ranked[1]);
        var tied = first.Alive == second.Alive && first.Deaths == second.Deaths;
        return new MatchResult(true, null, WinningTeam: tied ? null : first.Team);
    }

    // Time ran out: fewest deaths wins, then most health, then most hits landed; a full tie is a draw
    private static MatchResult ByHealthThenHits(List<Tank> alive)
    {
        var ranked = alive.OrderBy(tank => tank.Deaths).ThenByDescending(tank => tank.Health).ThenByDescending(tank => tank.HitsLanded).ToList();
        var (first, second) = (ranked[0], ranked[1]);
        var tied = first.Deaths == second.Deaths && first.Health == second.Health && first.HitsLanded == second.HitsLanded;
        return new MatchResult(true, tied ? null : first.Id);
    }
}
