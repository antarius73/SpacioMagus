namespace SpacioMagus.Core;

public sealed class LevelDefinition
{
    private readonly Terrain[] _cells;

    public LevelDefinition(
        string name,
        int width,
        int height,
        IReadOnlyList<Terrain> cells,
        Position magusStart,
        IReadOnlyList<MonsterSpawn> monsters,
        IReadOnlyList<Position> potions)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(monsters);
        ArgumentNullException.ThrowIfNull(potions);

        if (width <= 0 || height <= 0)
        {
            throw new LevelDefinitionException($"Dimensions invalides : {width} x {height}.");
        }

        if (cells.Count != width * height)
        {
            throw new LevelDefinitionException($"Le terrain contient {cells.Count} cases au lieu de {width * height}.");
        }

        Name = string.IsNullOrWhiteSpace(name) ? "Sans nom" : name;
        Width = width;
        Height = height;
        _cells = cells.ToArray();
        MagusStart = magusStart;
        Monsters = monsters.ToArray();
        Potions = potions.ToArray();
        Validate();
    }

    public string Name { get; }

    public int Width { get; }

    public int Height { get; }

    public IReadOnlyList<Terrain> Cells => _cells;

    public Position MagusStart { get; }

    public IReadOnlyList<MonsterSpawn> Monsters { get; }

    public IReadOnlyList<Position> Potions { get; }

    public static bool IsSolid(Terrain terrain) => terrain is Terrain.Wall or Terrain.Void;

    public bool InBounds(Position position) =>
        position.X >= 0 && position.Y >= 0 && position.X < Width && position.Y < Height;

    public Terrain TerrainAt(Position position) =>
        InBounds(position) ? _cells[position.Y * Width + position.X] : Terrain.Void;

    public IEnumerable<Position> Grilles()
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var position = new Position(x, y);
                if (TerrainAt(position) == Terrain.Grille)
                {
                    yield return position;
                }
            }
        }
    }

    public (Position First, Position Second) GrilleSides(Position grille)
    {
        if (TerrainAt(grille) != Terrain.Grille)
        {
            throw new LevelDefinitionException($"Aucune grille en {grille}.");
        }

        var up = grille + new Position(0, -1);
        var down = grille + new Position(0, 1);
        var left = grille + new Position(-1, 0);
        var right = grille + new Position(1, 0);

        if (IsSolid(TerrainAt(left)) && IsSolid(TerrainAt(right)) && !IsSolid(TerrainAt(up)) && !IsSolid(TerrainAt(down)))
        {
            return (up, down);
        }

        if (IsSolid(TerrainAt(up)) && IsSolid(TerrainAt(down)) && !IsSolid(TerrainAt(left)) && !IsSolid(TerrainAt(right)))
        {
            return (left, right);
        }

        throw new LevelDefinitionException($"La grille en {grille} doit être entre deux murs, avec un passage de chaque côté.");
    }

    private static bool IsOpenGround(Terrain terrain) => terrain is Terrain.Floor or Terrain.Stairs;

    private void Validate()
    {
        if (TerrainAt(MagusStart) != Terrain.Floor)
        {
            throw new LevelDefinitionException($"Le départ du magus {MagusStart} doit être une case de sol.");
        }

        if (!_cells.Contains(Terrain.Stairs))
        {
            throw new LevelDefinitionException("Le niveau doit contenir au moins un escalier.");
        }

        var occupied = new HashSet<Position> { MagusStart };
        foreach (var spawn in Monsters)
        {
            if (!IsOpenGround(TerrainAt(spawn.Start)))
            {
                throw new LevelDefinitionException($"Le monstre en {spawn.Start} doit commencer sur une case de sol.");
            }

            if (!IsOpenGround(TerrainAt(spawn.Post)))
            {
                throw new LevelDefinitionException($"Le poste {spawn.Post} du monstre en {spawn.Start} doit être une case de sol.");
            }

            if (!occupied.Add(spawn.Start))
            {
                throw new LevelDefinitionException($"Deux créatures commencent sur la même case {spawn.Start}.");
            }
        }

        var potions = new HashSet<Position>();
        foreach (var potion in Potions)
        {
            if (TerrainAt(potion) != Terrain.Floor)
            {
                throw new LevelDefinitionException($"La fiole en {potion} doit être sur une case de sol.");
            }

            if (!potions.Add(potion))
            {
                throw new LevelDefinitionException($"Deux fioles sur la même case {potion}.");
            }
        }

        foreach (var grille in Grilles())
        {
            GrilleSides(grille);
        }
    }
}
