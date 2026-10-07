namespace GameLogic;

public static class MatchSummary
{
    // Final standings for the end screen: the winner, then everyone still in (fewest deaths, most health),
    // then the eliminated, latest out first. Kills and hits landed break the remaining ties
    public static IReadOnlyList<Tank> Rank(IEnumerable<Tank> tanks, Guid? winnerId) =>
        tanks
            .OrderByDescending(tank => tank.Id == winnerId)
            .ThenBy(tank => tank.Eliminated)
            .ThenByDescending(tank => tank.EliminatedAtTick ?? int.MaxValue)
            .ThenBy(tank => tank.Deaths)
            .ThenByDescending(tank => tank.Health)
            .ThenByDescending(tank => tank.Kills)
            .ThenByDescending(tank => tank.HitsLanded)
            .ToList();
}
