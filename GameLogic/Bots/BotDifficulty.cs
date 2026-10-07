namespace GameLogic;

public enum BotDifficulty { Easy, Medium, Hard }

// Every number that makes one difficulty play differently from another; bots read these each tick,
// so changing the difficulty mid-match takes effect on the next tick
public record BotProfile(int ReactionTicks, double AimErrorDegrees, double LeadFactor, double EvadeChance)
{
    public static BotProfile For(BotDifficulty difficulty) => difficulty switch
    {
        BotDifficulty.Easy => new BotProfile(ReactionTicks: 6, AimErrorDegrees: 15, LeadFactor: 0, EvadeChance: 0),
        BotDifficulty.Hard => new BotProfile(ReactionTicks: 1, AimErrorDegrees: 1, LeadFactor: 1, EvadeChance: 1),
        _ => new BotProfile(ReactionTicks: 3, AimErrorDegrees: 6, LeadFactor: 0.5, EvadeChance: 0.5),
    };
}
