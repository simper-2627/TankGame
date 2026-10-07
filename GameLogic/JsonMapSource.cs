using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameLogic;

public enum MapMode { BigMap = 1, Foggish = 2 }

public record ModeSettings(int Width, int Height, int ViewWidth, int ViewHeight, int MaxPlayers, int SpawnCount);

public class JsonMapSource : IMapSource
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
    public IReadOnlyList<GameMap> Maps { get; }
    public GameMap DefaultMap => Maps[0];
    public GameMap GetByName(string? mapName) => Maps.FirstOrDefault(m => m.Name == mapName) ?? DefaultMap;

    public JsonMapSource(IReadOnlyDictionary<MapMode, ModeSettings>? overrides = null)
    {
        var modes = Read<Dictionary<MapMode, ModeSettings>>("modes.json");
        if (overrides is not null)
            foreach (var (mode, settings) in overrides) modes[mode] = settings;
        var prefabs = Read<Dictionary<string, Obstacle[]>>("prefabs.json");
        var maps = new List<GameMap>();
        foreach (var resource in typeof(JsonMapSource).Assembly.GetManifestResourceNames()
                     .Where(n => n.Contains(".Maps.Layouts.")).OrderBy(n => n))
        {
            using var stream = typeof(JsonMapSource).Assembly.GetManifestResourceStream(resource)!;
            var layout = JsonSerializer.Deserialize<MapLayout>(stream, Options)!;
            var mode = modes[layout.Mode];
            // Layout coordinates use a reference canvas; changing mode size scales placements and spawns.
            var sx = (double)mode.Width / layout.Width;
            var sy = (double)mode.Height / layout.Height;
            Obstacle Scale(Obstacle shape, double x = 0, double y = 0, double scale = 1, string? color = null) =>
                new((int)Math.Round((x + shape.X * scale) * sx), (int)Math.Round((y + shape.Y * scale) * sy),
                    (int)Math.Round(shape.Width * scale * sx), (int)Math.Round(shape.Height * scale * sy))
                {
                    Shape = shape.Shape, Color = color ?? shape.Color, StartAngle = shape.StartAngle,
                    SweepAngle = shape.SweepAngle, InnerRatio = shape.InnerRatio
                };
            var obstacles = layout.Placements.SelectMany(p => prefabs[p.Prefab]
                .Select(shape => Scale(shape, p.X, p.Y, p.Scale, p.Color))).ToArray();
            var map = new GameMap(layout.Name, mode.Width, mode.Height, obstacles,
                layout.Spawns.Select(p => new MapSpawnPoint((int)Math.Round(p.X * sx), (int)Math.Round(p.Y * sy), p.Angle)).ToArray())
            {
                Mode = layout.Mode, MaxPlayers = mode.MaxPlayers, ViewWidth = mode.ViewWidth,
                ViewHeight = mode.ViewHeight, GroundColor = layout.GroundColor,
                GroundPatches = layout.GroundPatches.Select(p => Scale(p)).ToArray()
            };
            if (mode.ViewWidth <= 0 || mode.ViewHeight <= 0 || mode.MaxPlayers < 1 ||
                map.SpawnPoints.Count != mode.SpawnCount || map.SpawnPoints.Distinct().Count() != mode.SpawnCount ||
                map.SpawnPoints.Any(p => map.Blocks(new RectangleArea(p.X, p.Y - new DeveloperGameSettings().VisualTopOffset, Tank.Size, Tank.Size))))
                throw new InvalidDataException($"Invalid dimensions or blocked/missing spawns in {map.Name}");
            maps.Add(map);
        }
        Maps = maps;
    }

    private static T Read<T>(string suffix)
    {
        var assembly = typeof(JsonMapSource).Assembly;
        using var stream = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames()
            .Single(n => n.EndsWith(".Maps." + suffix)))!;
        return JsonSerializer.Deserialize<T>(stream, Options)!;
    }

    private record Placement(string Prefab, double X, double Y, double Scale = 1, string? Color = null);
    private record MapLayout(string Name, MapMode Mode, int Width, int Height, Placement[] Placements,
        MapSpawnPoint[] Spawns, Obstacle[] GroundPatches, string GroundColor);
}
