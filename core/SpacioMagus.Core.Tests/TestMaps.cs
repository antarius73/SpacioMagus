namespace SpacioMagus.Core.Tests;

internal static class TestMaps
{
    public static LevelDefinition Parse(params string[] lines)
    {
        var height = lines.Length;
        var width = lines.Max(line => line.Length);
        var cells = new Terrain[width * height];
        Position? magus = null;
        var monsters = new List<MonsterSpawn>();
        var potions = new List<Position>();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var symbol = x < lines[y].Length ? lines[y][x] : ' ';
                var position = new Position(x, y);
                cells[y * width + x] = symbol switch
                {
                    '#' => Terrain.Wall,
                    ' ' => Terrain.Void,
                    'G' => Terrain.Grille,
                    '>' => Terrain.Stairs,
                    '.' or '@' or 'b' or 'p' => Terrain.Floor,
                    _ => throw new ArgumentException($"Caractère inconnu '{symbol}' en {position}."),
                };

                switch (symbol)
                {
                    case '@':
                        magus = position;
                        break;
                    case 'b':
                        monsters.Add(new MonsterSpawn(MonsterKind.BlobVert, position, position));
                        break;
                    case 'p':
                        potions.Add(position);
                        break;
                }
            }
        }

        return new LevelDefinition(
            "test",
            width,
            height,
            cells,
            magus ?? throw new ArgumentException("Aucun départ de magus."),
            monsters,
            potions);
    }

    public static LevelDefinition WithPost(LevelDefinition level, Position start, Position post) =>
        new(
            level.Name,
            level.Width,
            level.Height,
            level.Cells,
            level.MagusStart,
            level.Monsters.Select(spawn => spawn.Start == start ? spawn with { Post = post } : spawn).ToList(),
            level.Potions);

    public static Game NewGame(LevelDefinition level, RuleSet? rules = null) =>
        new(level, rules, new FixedRandom());
}

internal sealed class FixedRandom : IRandomSource
{
    private readonly int _value;

    public FixedRandom(int value = 0)
    {
        _value = value;
    }

    public int Next(int maxExclusive) => Math.Min(_value, maxExclusive - 1);
}
