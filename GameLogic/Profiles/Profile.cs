namespace GameLogic.Profiles;

// What a player keeps between matches. The Id is the player's only credential (no login yet), so it is
// returned to its owner only and never put in game state that other players receive
public record Profile(
    Guid Id,
    string DisplayName,
    long Cash = 0,
    long SecondsPlayed = 0,
    int HitsLanded = 0,
    int Kills = 0);
