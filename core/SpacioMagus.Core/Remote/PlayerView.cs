namespace SpacioMagus.Core.Remote;

public sealed record PlayerView(
    string Level,
    int Width,
    int Height,
    int Turn,
    GameStatus Status,
    LossReason LossReason,
    bool Stranded,
    MagusView Magus,
    IReadOnlyList<string> Map,
    IReadOnlyList<MonsterView> Monsters,
    IReadOnlyList<GrilleView> Grilles,
    IReadOnlyList<Position> Potions,
    IReadOnlyList<ContainerView> Containers,
    IReadOnlyList<Position> Levers,
    IReadOnlyList<AlcoveView> Alcoves,
    IReadOnlyList<Position> Exits,
    FearView Fear,
    LegalActionsView Legal)
{
    public static PlayerView Capture(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);
        var level = game.Level;
        var alcoves = game.Script is null
            ? Array.Empty<AlcoveView>()
            : level.Alcoves.Select(alcove => new AlcoveView(alcove, game.IsActivated(alcove))).ToArray();
        var hasBlobs = game.Monsters.Any(monster => monster.Kind is MonsterKind.BlobVert or MonsterKind.BlobRouge);

        return new PlayerView(
            level.Name,
            level.Width,
            level.Height,
            game.Turn,
            game.Status,
            game.LossReason,
            game.IsStranded,
            new MagusView(game.Magus.Position, game.Magus.Magic, game.Rules.MagicMax, game.Rules.SpellCost, game.Rules.SpellRange),
            MapText.Rows(game),
            game.Monsters
                .Select(monster => new MonsterView(
                    monster.Id,
                    monster.Kind,
                    monster.Position,
                    monster.IsTracking,
                    monster.StunTurns,
                    monster.IsMutating))
                .ToArray(),
            game.Grilles
                .OrderBy(grille => grille.Position.Y)
                .ThenBy(grille => grille.Position.X)
                .Select(grille => new GrilleView(
                    grille.Position,
                    level.IsDoor(grille.Position) ? GrilleKind.Door : GrilleKind.Portcullis,
                    grille.IsOpen,
                    grille.TurnsRemaining))
                .ToArray(),
            Sorted(game.Potions),
            Sorted(game.Containers)
                .Select(container => new ContainerView(
                    container,
                    level.TerrainAt(container),
                    game.PotionsAreRevealed ? game.ContainerHasPotion(container) : null))
                .ToArray(),
            Sorted(level.CellsOf(Terrain.Lever)),
            alcoves,
            Sorted(level.CellsOf(Terrain.Stairs)),
            new FearView(
                hasBlobs ? Sorted(game.FearZone(MonsterKind.BlobVert)) : Array.Empty<Position>(),
                hasBlobs ? Sorted(game.FearZone(MonsterKind.BlobRouge)) : Array.Empty<Position>()),
            LegalActions.Compute(game));
    }

    private static IReadOnlyList<Position> Sorted(IEnumerable<Position> positions) =>
        positions.OrderBy(position => position.Y).ThenBy(position => position.X).ToArray();
}

public sealed record MagusView(Position Position, int Magic, int MagicMax, int SpellCost, int SpellRange);

public sealed record MonsterView(int Id, MonsterKind Kind, Position Position, bool Tracking, int StunTurns, bool Mutating);

public enum GrilleKind
{
    Door,
    Portcullis,
}

public sealed record GrilleView(Position Position, GrilleKind Kind, bool Open, int TurnsRemaining);

public sealed record ContainerView(Position Position, Terrain Kind, bool? Potion);

public sealed record AlcoveView(Position Position, bool Lit);

public sealed record FearView(IReadOnlyList<Position> GreenBlobAvoids, IReadOnlyList<Position> RedBlobAvoids);
