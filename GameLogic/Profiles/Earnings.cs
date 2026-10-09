namespace GameLogic.Profiles;

// A payout: what a player earned since their last one
public record Earnings(long Cash, long Seconds, int Hits, int Kills)
{
    public bool IsEmpty => Cash == 0 && Seconds == 0 && Hits == 0 && Kills == 0;
}

// Every earning rate in one place, so tuning the economy is a one-line change
public record CurrencyRates(int SecondsPerCash = 10, int CashPerHit = 2, int CashPerKill = 25)
{
    public static readonly CurrencyRates Default = new();
}
