using GameLogic.Game;

namespace GameLogic.Bots;

public enum BotState { Inactive, Seek, Attack, Unstuck, Evade }

// One bot's mind. Each tick it gets the same filtered view of the game a human player gets
// and answers with the same PlayerInputRequest a keyboard and mouse would send
public sealed class BotBrain
{
    // Ticks without line of sight before an attacking bot goes back to seeking
    public const int LostSightTicks = 5;
    public const double MinFightDistance = 250;
    public const double MaxFightDistance = 400;
    // 5 ticks reversing, then 5 ticks sideways
    public const int UnstuckTicks = 10;
    public const int EvadeTicks = 5;
    // A seeking bot plans a new route when its old one is this old, or the human has moved this far from where it led
    public const int RepathTicks = 10;
    public const double RepathDistance = 40;
    // A waypoint this close to the bot's center counts as reached
    public const double WaypointReachedDistance = 30;

    private readonly Random random;
    private readonly TargetTracker targets = new();

    private BotState fsm = BotState.Inactive;
    private Guid? previousTargetId;
    private (double X, double Y) previousTargetCenter;
    private int clearTicks;
    private int blockedTicks;
    private bool shootHeld;
    // -1..1, scaled by the difficulty's aim error; picked again after every shot
    private double aimError;
    private int strafeSign = 1;
    private int strafeTicksLeft;
    private readonly StuckDetector stuck = new();
    private Keys lastMove = Keys.None;
    private (double X, double Y) lastDirection;
    private BotState? overrideState;
    private int unstuckTicksLeft;
    private int unstuckSide = 1;
    private int evadeTicksLeft;
    private Keys evadeKeys;
    // Whether this bot decided to dodge a given bullet: rolled once per bullet, not once per tick
    private readonly Dictionary<Guid, bool> evadeRolls = [];
    // Seek's route around walls; planned is false when there's no route to follow (not planned yet, or out of date)
    private IReadOnlyList<(double X, double Y)>? path;
    private bool pathPlanned;
    private int pathAge;
    private int pathIndex;
    private (double X, double Y) pathGoal;

    public BotBrain(Guid tankId, Random random)
    {
        TankId = tankId;
        this.random = random;
        aimError = NextAimError();
    }

    public Guid TankId { get; }

    // What the bot is doing right now; shown above the tank in Developer simulation
    public BotState State => overrideState ?? fsm;

    public PlayerInputRequest Decide(GameState view, GameMap map)
    {
        overrideState = null;
        var profile = BotProfile.For(view.Settings.BotDifficulty);
        var dev = view.DeveloperSettings;
        var tanks = (view.Tanks ?? []).ToList();
        var me = tanks.FirstOrDefault(tank => tank.Id == TankId);
        var humans = tanks.Where(tank => !tank.IsBot && !tank.Eliminated && !tank.Respawning).ToList();

        if (me is null || me.Eliminated || me.Respawning || view.Status == GameStatus.Ended || humans.Count == 0)
            return Idle(view);

        var myCenter = BotSenses.Center(me, dev);
        var target = targets.Choose(humans, human => BotSenses.Distance(myCenter, BotSenses.Center(human, dev)))!;
        var targetCenter = BotSenses.Center(target, dev);
        // Per-tick velocity from the last position seen, used to lead the shot
        var targetVelocity = previousTargetId == target.Id
            ? (targetCenter.X - previousTargetCenter.X, targetCenter.Y - previousTargetCenter.Y)
            : (0.0, 0.0);
        previousTargetId = target.Id;
        previousTargetCenter = targetCenter;

        var toTarget = (X: targetCenter.X - myCenter.X, Y: targetCenter.Y - myCenter.Y);
        var distance = BotSenses.Distance(myCenter, targetCenter);
        var lineOfSight = BotSenses.HasLineOfSight(map, myCenter, targetCenter);
        clearTicks = lineOfSight ? clearTicks + 1 : 0;
        blockedTicks = lineOfSight ? 0 : blockedTicks + 1;

        if (fsm == BotState.Inactive)
            fsm = BotState.Seek;
        if (fsm == BotState.Seek && clearTicks >= profile.ReactionTicks)
            fsm = BotState.Attack;
        else if (fsm == BotState.Attack && blockedTicks >= LostSightTicks)
            fsm = BotState.Seek;

        if (fsm == BotState.Attack)
            pathPlanned = false;
        var direction = fsm == BotState.Attack
            ? AttackDirection(toTarget, distance)
            : SeekDirection(map, dev, myCenter, targetCenter, toTarget);
        var move = WithUnstuck(me, direction, BotSteering.Toward(direction.X, direction.Y));
        if (overrideState == BotState.Unstuck)
            evadeTicksLeft = 0;
        else
            move = WithEvade(view, myCenter, profile, move);
        lastMove = move;

        // The game only fires when Shoot goes from off to on, so every press is followed by a release
        var shoot = false;
        if (shootHeld)
            shootHeld = false;
        else if ((fsm == BotState.Attack || overrideState is BotState.Unstuck or BotState.Evade) && lineOfSight && (me.ReloadMsLeft ?? 0) == 0)
            shoot = shootHeld = true;

        var instantShot = view.Settings.Projectile == ProjectileType.Realistic;
        var aim = BotAim.AimPoint(myCenter, targetCenter, targetVelocity, view.Settings.BulletSpeed,
            instantShot ? 0 : profile.LeadFactor, aimError * profile.AimErrorDegrees);
        if (shoot)
            aimError = NextAimError();

        return Input(view, move, shoot, aim);
    }

    // Straight at the human when nothing is in the way; otherwise along a route around the walls
    private (double X, double Y) SeekDirection(GameMap map, DeveloperGameSettings dev, (double X, double Y) myCenter,
        (double X, double Y) targetCenter, (double X, double Y) toTarget)
    {
        if (BotPathfinder.IsClear(map, myCenter, targetCenter, dev))
        {
            pathPlanned = false;
            return toTarget;
        }

        // Rate-limited so a few bots planning routes stay cheap; a failed plan is kept just as long
        if (!pathPlanned || pathAge >= RepathTicks || BotSenses.Distance(targetCenter, pathGoal) > RepathDistance)
        {
            path = BotPathfinder.FindPath(map, myCenter, targetCenter, dev);
            pathPlanned = true;
            pathAge = 0;
            pathIndex = 0;
            pathGoal = targetCenter;
        }
        pathAge++;

        while (path is not null && pathIndex < path.Count &&
               BotSenses.Distance(myCenter, path[pathIndex]) <= WaypointReachedDistance)
            pathIndex++;
        if (path is null || pathIndex >= path.Count)
            return toTarget;
        var waypoint = path[pathIndex];
        return (waypoint.X - myCenter.X, waypoint.Y - myCenter.Y);
    }

    // Too close: back off. Too far: close in. In range: strafe, switching side every 1-2 seconds
    private (double X, double Y) AttackDirection((double X, double Y) toTarget, double distance)
    {
        if (distance < MinFightDistance)
            return (-toTarget.X, -toTarget.Y);
        if (distance > MaxFightDistance)
            return toTarget;
        if (strafeTicksLeft-- <= 0)
        {
            strafeSign = random.Next(2) == 0 ? -1 : 1;
            strafeTicksLeft = random.Next(10, 21);
        }
        return (-toTarget.Y * strafeSign, toTarget.X * strafeSign);
    }

    // Pushing a key but going nowhere: back up for a moment, then sidestep, then carry on with what it was doing
    private Keys WithUnstuck(TankState me, (double X, double Y) wanted, Keys move)
    {
        if (unstuckTicksLeft == 0 && stuck.Update(me.PositionX, me.PositionY, lastMove.Any))
        {
            unstuckTicksLeft = UnstuckTicks;
            unstuckSide = random.Next(2) == 0 ? -1 : 1;
            // Whatever route got it wedged is no good from here
            pathPlanned = false;
        }
        if (unstuckTicksLeft == 0)
        {
            lastDirection = wanted;
            return move;
        }

        overrideState = BotState.Unstuck;
        var reversing = UnstuckTicks - unstuckTicksLeft < UnstuckTicks / 2;
        unstuckTicksLeft--;
        return reversing
            ? BotSteering.Toward(-lastDirection.X, -lastDirection.Y)
            : BotSteering.Toward(-lastDirection.Y * unstuckSide, lastDirection.X * unstuckSide);
    }

    // A travelling bullet about to hit: step sideways out of its line for a moment. Instant shots can't be dodged
    private Keys WithEvade(GameState view, (double X, double Y) myCenter, BotProfile profile, Keys move)
    {
        var bullets = view.Bullets ?? [];
        foreach (var gone in evadeRolls.Keys.Where(id => !bullets.Any(bullet => bullet.Id == id)).ToList())
            evadeRolls.Remove(gone);

        if (evadeTicksLeft == 0 && view.Settings.Projectile == ProjectileType.DumbBubbles && profile.EvadeChance > 0)
        {
            foreach (var bullet in bullets.Where(bullet => bullet.OwnerId != TankId))
            {
                if (BotSenses.Threat(bullet, myCenter, view.Settings.BulletSpeed) is not { } dodge)
                    continue;
                if (!evadeRolls.TryGetValue(bullet.Id, out var willDodge))
                    evadeRolls[bullet.Id] = willDodge = random.NextDouble() < profile.EvadeChance;
                if (!willDodge)
                    continue;
                evadeTicksLeft = EvadeTicks;
                evadeKeys = BotSteering.Toward(dodge.X, dodge.Y);
                break;
            }
        }

        if (evadeTicksLeft == 0)
            return move;
        evadeTicksLeft--;
        overrideState = BotState.Evade;
        return evadeKeys;
    }

    private double NextAimError() => random.NextDouble() * 2 - 1;

    private PlayerInputRequest Input(GameState view, Keys move, bool shoot, (int X, int Y)? aim) => new()
    {
        GameName = view.Name ?? "",
        PlayerId = TankId,
        Up = move.Up,
        Down = move.Down,
        Left = move.Left,
        Right = move.Right,
        Shoot = shoot,
        Boost = false,
        AimX = aim?.X,
        AimY = aim?.Y,
    };

    // Dead, nobody to fight, or the match is over: press nothing and forget the fight
    private PlayerInputRequest Idle(GameState view)
    {
        fsm = BotState.Inactive;
        targets.Reset();
        previousTargetId = null;
        clearTicks = 0;
        blockedTicks = 0;
        shootHeld = false;
        stuck.Reset();
        lastMove = Keys.None;
        unstuckTicksLeft = 0;
        evadeTicksLeft = 0;
        evadeRolls.Clear();
        path = null;
        pathPlanned = false;
        pathAge = 0;
        pathIndex = 0;
        overrideState = null;
        return Input(view, Keys.None, shoot: false, aim: null);
    }
}
