namespace GameLogic;

public static class PlayerNames
{
    private static readonly string[] Adjectives = ["Iron", "Steel", "Crimson", "Thunder", "Dust", "Rolling", "Silent", "Scorched", "Midnight", "Rusty"];
    private static readonly string[] Nouns = ["Viper", "Storm", "Hammer", "Rhino", "Anvil", "Fury", "Badger", "Tread", "Cyclone", "Bastion"];

    public static string Generate(IEnumerable<string> taken, Random random)
    {
        var used = taken.ToHashSet();
        string name;
        var attempts = 0;
        do
        {
            name = $"{Adjectives[random.Next(Adjectives.Length)]} {Nouns[random.Next(Nouns.Length)]}";
            // 100 combinations: past a few tries, number it instead of looping on a crowded game
            if (++attempts > 20) name += $" {random.Next(2, 1000)}";
        } while (used.Contains(name));
        return name;
    }
}
