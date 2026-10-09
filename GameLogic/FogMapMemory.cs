namespace GameLogic;

// Each client owns its exploration history; offscreen snapshots never move remembered markers.
public sealed class FogMapMemory
{
    private const int CellSize = 40;
    private (double Left, double Top, double Right, double Bottom)? previousView;
    private readonly HashSet<(int X, int Y)> explored = [];
    private readonly Dictionary<Guid, MapContact> contacts = [];
    private readonly Dictionary<Guid, MapContact> teammates = [];
    public IReadOnlyDictionary<Guid, MapContact> Contacts => contacts;
    public IReadOnlyDictionary<Guid, MapContact> Teammates => teammates;
    public string ExploredPath { get; private set; } = "";

    public void Observe(GameMap map, MapCamera camera, double width, double height,
        IEnumerable<TankState> tanks, Guid viewer, int visualTopOffset)
    {
        var right = Math.Min(map.Width, camera.X + width / camera.Scale);
        var bottom = Math.Min(map.Height, camera.Y + height / camera.Scale);
        bool InView(double x, double y) => x >= camera.X && x <= right && y >= camera.Y && y <= bottom;
        var changed = false;
        for (var y = (int)(camera.Y / CellSize); y < Math.Ceiling(bottom / CellSize); y++)
            for (var x = (int)(camera.X / CellSize); x < Math.Ceiling(right / CellSize); x++)
                changed |= explored.Add((x, y));
        if (changed)
        {
            var path = new System.Text.StringBuilder();
            foreach (var row in explored.GroupBy(cell => cell.Y).OrderBy(row => row.Key))
            {
                var columns = row.Select(cell => cell.X).Order().ToArray();
                for (var i = 0; i < columns.Length; i++)
                {
                    var start = columns[i];
                    var end = start + 1;
                    while (i + 1 < columns.Length && columns[i + 1] == end) { end++; i++; }
                    path.Append($"M{start * CellSize} {row.Key * CellSize}h{(end - start) * CellSize}v{CellSize}h{-((end - start) * CellSize)}Z ");
                }
            }
            ExploredPath = path.ToString();
        }

        var tankList = tanks as IReadOnlyCollection<TankState> ?? tanks.ToArray();
        var viewerTeam = tankList.FirstOrDefault(t => t.Id == viewer)?.Team;

        var visible = tankList.Where(t => t.Id != viewer && !t.Eliminated && !t.Respawning
                                     && viewerTeam is null || t.Team != viewerTeam)
            .Select(t => new MapContact(t.Id, t.PositionX + Tank.Size / 2.0,
                t.PositionY - visualTopOffset + Tank.Size / 2.0, true))
            .Where(t => InView(t.X, t.Y)).ToDictionary(t => t.Id);
        foreach (var contact in contacts.Values.ToArray())
        {
            if (visible.ContainsKey(contact.Id)) continue;
            // Revisiting an empty last-known location clears the stale report.
            var wasInView = previousView is { } old && contact.X >= old.Left && contact.X <= old.Right
                && contact.Y >= old.Top && contact.Y <= old.Bottom;
            if (!contact.Visible && !wasInView && InView(contact.X, contact.Y)) contacts.Remove(contact.Id);
            else contacts[contact.Id] = contact with { Visible = false };
        }
        foreach (var contact in visible.Values) contacts[contact.Id] = contact;
        previousView = (camera.X, camera.Y, right, bottom);

        // Teammates: always current, never decayed, regardless of explored/view state
        teammates.Clear();
        if (viewerTeam is int team)
        {
            foreach (var t in tankList)
            {
                if (t.Id == viewer || t.Team != team || t.Eliminated || t.Respawning) continue;
                teammates[t.Id] = new MapContact(t.Id, t.PositionX + Tank.Size / 2.0,
                    t.PositionY - visualTopOffset + Tank.Size / 2.0, true);
            }
        }
    }
}

public record MapContact(Guid Id, double X, double Y, bool Visible);
