namespace SpacioMagus.Core;

public sealed class LevelDefinition
{
    private readonly Terrain[] _cells;
    private readonly HashSet<Position> _doors;

    public LevelDefinition(
        string name,
        int width,
        int height,
        IReadOnlyList<Terrain> cells,
        Position magusStart,
        IReadOnlyList<MonsterSpawn> monsters,
        IReadOnlyList<Position> potions,
        IReadOnlyList<Position>? torches = null,
        IReadOnlyList<Position>? doors = null,
        IReadOnlyList<Position>? alcoves = null,
        IReadOnlyDictionary<Position, Position>? levers = null,
        string? script = null)
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
        Torches = (torches ?? Array.Empty<Position>()).ToArray();
        Doors = (doors ?? Array.Empty<Position>()).ToArray();
        _doors = new HashSet<Position>(Doors);
        Alcoves = (alcoves ?? Array.Empty<Position>()).ToArray();
        Levers = new Dictionary<Position, Position>(levers ?? new Dictionary<Position, Position>());
        Script = string.IsNullOrWhiteSpace(script) ? null : script;
        Validate();
    }

    public string Name { get; }

    public string? Script { get; }

    public int Width { get; }

    public int Height { get; }

    public IReadOnlyList<Terrain> Cells => _cells;

    public Position MagusStart { get; }

    public IReadOnlyList<MonsterSpawn> Monsters { get; }

    public IReadOnlyList<Position> Potions { get; }

    public IReadOnlyList<Position> Torches { get; }

    public IReadOnlyList<Position> Doors { get; }

    public IReadOnlyList<Position> Alcoves { get; }

    public IReadOnlyDictionary<Position, Position> Levers { get; }

    public Position? LeverTarget(Position lever) => Levers.TryGetValue(lever, out var grille) ? grille : null;

    public bool IsDoor(Position position) => _doors.Contains(position);

    public static bool IsObstacle(Terrain terrain) => terrain is Terrain.Brazier or Terrain.Water or Terrain.Lever or Terrain.StairsUp;

    public static bool IsContainer(Terrain terrain) => terrain is Terrain.Barrel or Terrain.Crate or Terrain.Vase;

    public static bool IsGround(Terrain terrain) =>
        terrain is Terrain.Floor or Terrain.Stairs || IsObstacle(terrain) || IsContainer(terrain);

    public static bool IsSolid(Terrain terrain) => terrain is Terrain.Wall or Terrain.Void;

    public static bool IsFlyable(Terrain terrain) =>
        terrain is Terrain.Floor or Terrain.Stairs or Terrain.Arch or Terrain.Water
            or Terrain.Barrel or Terrain.Crate or Terrain.Vase or Terrain.Lever;

    private static bool CanStart(MonsterKind kind, Terrain terrain) =>
        kind == MonsterKind.ChauveSouris ? IsFlyable(terrain) : IsOpenGround(terrain);

    public bool InBounds(Position position) =>
        position.X >= 0 && position.Y >= 0 && position.X < Width && position.Y < Height;

    public Terrain TerrainAt(Position position) =>
        InBounds(position) ? _cells[position.Y * Width + position.X] : Terrain.Void;

    public IEnumerable<Position> Grilles() => CellsOf(Terrain.Grille);

    public IEnumerable<Position> CellsOf(Terrain terrain)
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var position = new Position(x, y);
                if (TerrainAt(position) == terrain)
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

        return Passage(grille, "La grille");
    }

    private (Position First, Position Second) Passage(Position cell, string label)
    {
        var up = cell + new Position(0, -1);
        var down = cell + new Position(0, 1);
        var left = cell + new Position(-1, 0);
        var right = cell + new Position(1, 0);

        if (IsSolid(TerrainAt(left)) && IsSolid(TerrainAt(right)) && !IsSolid(TerrainAt(up)) && !IsSolid(TerrainAt(down)))
        {
            return (up, down);
        }

        if (IsSolid(TerrainAt(up)) && IsSolid(TerrainAt(down)) && !IsSolid(TerrainAt(left)) && !IsSolid(TerrainAt(right)))
        {
            return (left, right);
        }

        throw new LevelDefinitionException($"{label} en {cell} doit être entre deux murs, avec un passage de chaque côté.");
    }

    private static bool IsOpenGround(Terrain terrain) => terrain is Terrain.Floor or Terrain.Stairs or Terrain.Arch;

    private void ValidateRoute(MonsterSpawn spawn)
    {
        var route = spawn.Patrol;
        for (var index = 0; index < route.Count; index++)
        {
            var cell = route[index];
            if (!CanStart(spawn.Kind, TerrainAt(cell)))
            {
                throw new LevelDefinitionException(spawn.Kind == MonsterKind.ChauveSouris
                    ? $"La ronde de la chauve-souris en {spawn.Start} passe par {cell}, qu'elle ne peut pas survoler."
                    : $"La ronde du monstre en {spawn.Start} passe par {cell}, qui n'est pas une case de sol.");
            }

            var next = route[(index + 1) % route.Count];
            if (cell.ChebyshevTo(next) > 1)
            {
                throw new LevelDefinitionException($"La ronde du monstre en {spawn.Start} saute de {cell} à {next} : les cases doivent être voisines.");
            }

            var delta = next - cell;
            if (delta.X != 0 && delta.Y != 0
                && (IsSolid(TerrainAt(new Position(next.X, cell.Y))) || IsSolid(TerrainAt(new Position(cell.X, next.Y)))))
            {
                throw new LevelDefinitionException($"La ronde du monstre en {spawn.Start} coupe un coin de mur entre {cell} et {next}.");
            }
        }
    }

    private void Validate()
    {
        foreach (var door in Doors.Where(door => TerrainAt(door) != Terrain.Grille))
        {
            throw new LevelDefinitionException($"La porte en {door} doit être sur une case de grille.");
        }

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
            if (!CanStart(spawn.Kind, TerrainAt(spawn.Start)))
            {
                throw new LevelDefinitionException($"Le monstre en {spawn.Start} doit commencer sur une case de sol.");
            }

            if (!CanStart(spawn.Kind, TerrainAt(spawn.Post)))
            {
                throw new LevelDefinitionException($"Le poste {spawn.Post} du monstre en {spawn.Start} doit être une case de sol.");
            }

            ValidateRoute(spawn);

            if (!occupied.Add(spawn.Start))
            {
                throw new LevelDefinitionException($"Deux créatures commencent sur la même case {spawn.Start}.");
            }
        }

        var potions = new HashSet<Position>();
        foreach (var potion in Potions)
        {
            if (TerrainAt(potion) != Terrain.Floor && !IsContainer(TerrainAt(potion)))
            {
                throw new LevelDefinitionException($"La fiole en {potion} doit être sur une case de sol ou dans un contenant.");
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

        var torches = new HashSet<Position>();
        foreach (var torch in Torches)
        {
            if (TerrainAt(torch) != Terrain.Wall || !IsGround(TerrainAt(torch + new Position(0, 1))))
            {
                throw new LevelDefinitionException($"La torchère en {torch} doit être sur un mur avec du sol juste en dessous.");
            }

            if (!torches.Add(torch))
            {
                throw new LevelDefinitionException($"Deux torchères sur la même case {torch}.");
            }
        }

        foreach (var arch in CellsOf(Terrain.Arch))
        {
            Passage(arch, "L'arche");
        }

        foreach (var (lever, grille) in Levers)
        {
            if (TerrainAt(lever) != Terrain.Lever)
            {
                throw new LevelDefinitionException($"La manette déclarée en {lever} n'est pas sur une case de manette.");
            }

            if (TerrainAt(grille) != Terrain.Grille || IsDoor(grille))
            {
                throw new LevelDefinitionException($"La manette en {lever} doit commander une herse : {grille} n'en est pas une.");
            }
        }

        foreach (var lever in CellsOf(Terrain.Lever).Where(lever => !Levers.ContainsKey(lever)))
        {
            throw new LevelDefinitionException($"La manette en {lever} ne commande aucune herse : ajoutez-la dans [manettes].");
        }

        var alcoves = new HashSet<Position>();
        foreach (var alcove in Alcoves)
        {
            if (TerrainAt(alcove) != Terrain.Wall || !IsGround(TerrainAt(alcove + new Position(0, 1))))
            {
                throw new LevelDefinitionException($"L'alcôve en {alcove} doit être sur un mur avec du sol juste en dessous.");
            }

            if (!alcoves.Add(alcove))
            {
                throw new LevelDefinitionException($"Deux alcôves sur la même case {alcove}.");
            }
        }
    }
}
