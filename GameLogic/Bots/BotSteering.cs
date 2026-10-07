namespace GameLogic.Bots;

// The four movement keys a bot can press, like a human's keyboard
public readonly record struct Keys(bool Up, bool Down, bool Left, bool Right)
{
    public static readonly Keys None = new(false, false, false, false);
    public bool Any => Up || Down || Left || Right;
}

public static class BotSteering
{
    // The closest of the 8 key combinations to the wanted direction. Screen coordinates: +Y points down.
    // The tank's own rules handle turning the hull and backing up
    public static Keys Toward(double dx, double dy)
    {
        if (dx == 0 && dy == 0)
            return Keys.None;

        var sector = (int)Math.Round(Math.Atan2(dy, dx) / (Math.PI / 4));
        var wrapped = ((sector % 8) + 8) % 8;
        return wrapped switch
        {
            0 => new Keys(false, false, false, true),
            1 => new Keys(false, true, false, true),
            2 => new Keys(false, true, false, false),
            3 => new Keys(false, true, true, false),
            4 => new Keys(false, false, true, false),
            5 => new Keys(true, false, true, false),
            6 => new Keys(true, false, false, false),
            _ => new Keys(true, false, false, true),
        };
    }
}

// A tank that keeps pushing a key but goes nowhere is wedged against something
public sealed class StuckDetector
{
    // Ticks of pushing without moving before the bot counts as stuck
    public const int Window = 8;
    public const double MinMovePixels = 2;

    private double anchorX;
    private double anchorY;
    private int ticks;

    // Call once per tick with the tank's position and whether a movement key was pressed last tick
    public bool Update(double x, double y, bool pressedMove)
    {
        if (!pressedMove)
        {
            Reset();
            return false;
        }

        var moved = Math.Sqrt((x - anchorX) * (x - anchorX) + (y - anchorY) * (y - anchorY));
        if (ticks == 0 || moved >= MinMovePixels)
        {
            anchorX = x;
            anchorY = y;
            ticks = 1;
            return false;
        }

        if (++ticks < Window)
            return false;
        Reset();
        return true;
    }

    public void Reset() => ticks = 0;
}
