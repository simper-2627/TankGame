using GameLogic.Profiles;

namespace GameLogic;

// One row of the end-of-match leaderboard. Cash is null for a tank that earns nothing (bots, players without a profile)
public record StandingState(Guid TankId, string Name, bool IsBot, int? Team, bool Won, int Kills, int Hits, long? Cash);

public static class Standings
{
    public const int Shown = 3;

    // Winners first, then most kills, then most hits. A draw has no winners
    public static StandingState[] Build(IEnumerable<Tank> tanks, MatchResult result, Func<Tank, Earnings?> earned) =>
        tanks
            .Select(tank => (Tank: tank, Won: HasWon(tank, result)))
            .OrderByDescending(entry => entry.Won)
            .ThenByDescending(entry => entry.Tank.Kills)
            .ThenByDescending(entry => entry.Tank.HitsLanded)
            .Take(Shown)
            .Select(entry => new StandingState(entry.Tank.Id, entry.Tank.Name, entry.Tank.IsBot, entry.Tank.Team,
                entry.Won, entry.Tank.Kills, entry.Tank.HitsLanded, earned(entry.Tank)?.Cash))
            .ToArray();

    private static bool HasWon(Tank tank, MatchResult result) =>
        result.WinningTeam is int team ? tank.Team == team
        : result.BotsWon ? tank.IsBot
        : result.WinnerId is { } winner && tank.Id == winner;
}
