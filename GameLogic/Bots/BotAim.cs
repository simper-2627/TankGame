namespace GameLogic.Bots;

public static class BotAim
{
    // Keeps the aim point far enough away that rounding to whole pixels can't bend the angle
    private const double MinAimDistance = 50;

    // The point the turret should face. lead (0..1) is how much of the target's own movement to account for
    // while the bullet flies; errorDegrees is how far to miss by (positive turns clockwise on screen)
    public static (int X, int Y) AimPoint((double X, double Y) from, (double X, double Y) target,
        (double X, double Y) targetVelocity, double bulletSpeed, double lead, double errorDegrees)
    {
        var predicted = target;
        if (lead > 0 && bulletSpeed > 0)
        {
            var flightTicks = BotSenses.Distance(from, target) / bulletSpeed;
            predicted = (target.X + targetVelocity.X * flightTicks * lead, target.Y + targetVelocity.Y * flightTicks * lead);
        }

        var (dx, dy) = (predicted.X - from.X, predicted.Y - from.Y);
        var distance = Math.Max(MinAimDistance, Math.Sqrt(dx * dx + dy * dy));
        var angle = Math.Atan2(dy, dx) + errorDegrees * Math.PI / 180.0;
        return ((int)Math.Round(from.X + distance * Math.Cos(angle)), (int)Math.Round(from.Y + distance * Math.Sin(angle)));
    }
}
