namespace GameFrontend;

// A tank's color is derived from its id (or its team in a team match), and its bullets are drawn in the matching color.
// Red is kept for bots so a computer tank can be told apart at a glance
public static class TankPalette
{
    private static readonly string[] colors = ["Beige", "Black", "Blue", "Green"];
    public const string BotColor = "Red";

    public static string TankColor(Guid tankId, bool isBot = false, int? team = null) =>
        isBot ? BotColor : team is int t ? GameLogic.Teams.Name(t) : colors[tankId.ToByteArray()[0] % colors.Length];

    // The bullet sprites have no black variant, so black tanks fire silver
    public static string BulletColor(Guid ownerId, bool ownerIsBot = false, int? ownerTeam = null) => TankColor(ownerId, ownerIsBot, ownerTeam) switch
    {
        "Black" => "Silver",
        var color => color
    };
}
