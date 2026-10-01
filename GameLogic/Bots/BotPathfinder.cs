namespace GameLogic.Bots;

// Finds a way around walls for a seeking bot. Everything is in tank-center coordinates (what BotSenses.Center gives)
// and every point is tested with a tank-sized box, so a path only goes where the tank's hull actually fits
public static class BotPathfinder
{
    public const int CellSize = 20;
    // Extra room on each side of the hull so paths don't graze walls
    public const int Margin = 4;
    // Smaller than the hull, so a segment check can't step over a corner
    public const double SampleStep = 10;
    // How many cells away a start or goal inside a wall looks for a free cell
    public const int SnapRadius = 4;

    private const double Diagonal = 1.4142135623730951;

    // Waypoints (tank-center coordinates) from `from` to `to`, or null when `to` can't be reached.
    // The list leaves out the start and ends at the goal, or at the free cell next to it when the goal itself is tight
    public static IReadOnlyList<(double X, double Y)>? FindPath(GameMap map, (double X, double Y) from, (double X, double Y) to,
        DeveloperGameSettings dev)
    {
        var grid = new Grid(map, BoxSize(dev));
        var start = grid.Snap(from);
        var goal = grid.Snap(to);
        if (start < 0 || goal < 0)
            return null;

        var cells = grid.Search(start, goal);
        if (cells is null)
            return null;

        // The raw route: where the bot is, every cell center on the way, then the goal itself if the hull fits there
        var points = new List<(double X, double Y)>(cells.Count + 2) { from };
        points.AddRange(cells.Select(grid.CenterOf));
        if (grid.Fits(to) && IsClear(map, points[^1], to, grid.Box))
            points.Add(to);
        return StringPull(map, points, grid.Box);
    }

    // True when a tank-sized box can slide along the straight segment without touching a wall
    public static bool IsClear(GameMap map, (double X, double Y) from, (double X, double Y) to, DeveloperGameSettings dev) =>
        IsClear(map, from, to, BoxSize(dev));

    private static bool IsClear(GameMap map, (double X, double Y) from, (double X, double Y) to, int box)
    {
        var steps = Math.Max(1, (int)Math.Ceiling(BotSenses.Distance(from, to) / SampleStep));
        for (var i = 0; i <= steps; i++)
        {
            var t = i / (double)steps;
            if (!Fits(map, (from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t), box))
                return false;
        }
        return true;
    }

    // The hull's collision box (Tank.GetCollisionArea) plus the safety margin on each side
    private static int BoxSize(DeveloperGameSettings dev) =>
        Tank.Size - 2 * Math.Clamp(dev.HitboxInset, 0, Tank.Size / 2 - 1) + 2 * Margin;

    private static bool Fits(GameMap map, (double X, double Y) center, int box) =>
        !map.Blocks(new RectangleArea((int)Math.Floor(center.X - box / 2.0), (int)Math.Floor(center.Y - box / 2.0), box, box));

    // From each point, keep extending the leg while the next point is still in a clear line (stopping at the first
    // blocked one), so the bot drives a few long legs instead of zig-zagging cell by cell. Walking forward keeps it
    // to about one check per point
    private static List<(double X, double Y)> StringPull(GameMap map, List<(double X, double Y)> points, int box)
    {
        var waypoints = new List<(double X, double Y)>();
        var at = 0;
        while (at < points.Count - 1)
        {
            var next = at + 1;
            while (next + 1 < points.Count && IsClear(map, points[at], points[next + 1], box))
                next++;
            waypoints.Add(points[next]);
            at = next;
        }
        return waypoints;
    }

    // The map cut into CellSize squares. A cell is walkable when the box fits centered on it; that's only worked out
    // for the cells the search actually touches
    private sealed class Grid(GameMap map, int box)
    {
        private readonly int columns = Math.Max(1, map.Width / CellSize);
        private readonly int rows = Math.Max(1, map.Height / CellSize);
        // 0 = not checked yet, 1 = free, 2 = blocked
        private sbyte[]? walkable;

        public int Box => box;

        public (double X, double Y) CenterOf(int cell) =>
            ((cell % columns + 0.5) * CellSize, (cell / columns + 0.5) * CellSize);

        public bool Fits((double X, double Y) center) => BotPathfinder.Fits(map, center, box);

        private bool Walkable(int column, int row)
        {
            if (column < 0 || row < 0 || column >= columns || row >= rows)
                return false;
            walkable ??= new sbyte[columns * rows];
            var cell = row * columns + column;
            if (walkable[cell] == 0)
                walkable[cell] = (sbyte)(Fits(CenterOf(cell)) ? 1 : 2);
            return walkable[cell] == 1;
        }

        // The point's own cell, or the nearest walkable cell within SnapRadius; -1 when there is none
        public int Snap((double X, double Y) point)
        {
            var column = Math.Clamp((int)(point.X / CellSize), 0, columns - 1);
            var row = Math.Clamp((int)(point.Y / CellSize), 0, rows - 1);
            if (Walkable(column, row))
                return row * columns + column;

            var best = -1;
            var bestDistance = double.MaxValue;
            for (var r = Math.Max(0, row - SnapRadius); r <= Math.Min(rows - 1, row + SnapRadius); r++)
                for (var c = Math.Max(0, column - SnapRadius); c <= Math.Min(columns - 1, column + SnapRadius); c++)
                {
                    if (!Walkable(c, r))
                        continue;
                    var distance = BotSenses.Distance(point, CenterOf(r * columns + c));
                    if (distance < bestDistance)
                        (best, bestDistance) = (r * columns + c, distance);
                }
            return best;
        }

        // A* with 8 neighbours. A diagonal step needs both side cells free, so the hull never cuts a wall's corner
        public List<int>? Search(int start, int goal)
        {
            var cost = new double[columns * rows];
            Array.Fill(cost, double.MaxValue);
            var cameFrom = new int[columns * rows];
            var done = new bool[columns * rows];
            var open = new PriorityQueue<int, double>();
            cost[start] = 0;
            cameFrom[start] = -1;
            open.Enqueue(start, Estimate(start, goal));

            while (open.TryDequeue(out var cell, out _))
            {
                if (cell == goal)
                    return Route(cameFrom, goal);
                if (done[cell])
                    continue;
                done[cell] = true;

                var (column, row) = (cell % columns, cell / columns);
                for (var dy = -1; dy <= 1; dy++)
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        if ((dx == 0 && dy == 0) || !Walkable(column + dx, row + dy))
                            continue;
                        if (dx != 0 && dy != 0 && (!Walkable(column + dx, row) || !Walkable(column, row + dy)))
                            continue;
                        var next = (row + dy) * columns + column + dx;
                        var nextCost = cost[cell] + (dx != 0 && dy != 0 ? Diagonal : 1);
                        if (done[next] || nextCost >= cost[next])
                            continue;
                        cost[next] = nextCost;
                        cameFrom[next] = cell;
                        open.Enqueue(next, nextCost + Estimate(next, goal));
                    }
            }
            return null;
        }

        // Octile distance in cells: exact on an empty grid, so A* never overestimates
        private double Estimate(int from, int to)
        {
            var dx = Math.Abs(from % columns - to % columns);
            var dy = Math.Abs(from / columns - to / columns);
            return Math.Max(dx, dy) + (Diagonal - 1) * Math.Min(dx, dy);
        }

        private static List<int> Route(int[] cameFrom, int goal)
        {
            var route = new List<int>();
            for (var cell = goal; cell >= 0; cell = cameFrom[cell])
                route.Add(cell);
            route.Reverse();
            return route;
        }
    }
}
