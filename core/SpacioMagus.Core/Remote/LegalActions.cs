namespace SpacioMagus.Core.Remote;

public sealed record LegalActionsView(
    bool Wait,
    IReadOnlyList<Position> Move,
    IReadOnlyList<Position> OpenGrille,
    IReadOnlyList<Position> Break,
    IReadOnlyList<Position> Lever,
    IReadOnlyList<Position> Activate,
    IReadOnlyList<Position> SpacioMagus,
    IReadOnlyList<Position> SpacioCreatura,
    IReadOnlyList<Position>? Chest = null)
{
    public static LegalActionsView None { get; } = new(
        false,
        Array.Empty<Position>(),
        Array.Empty<Position>(),
        Array.Empty<Position>(),
        Array.Empty<Position>(),
        Array.Empty<Position>(),
        Array.Empty<Position>(),
        Array.Empty<Position>(),
        Array.Empty<Position>());
}

public static class LegalActions
{
    public static LegalActionsView Compute(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (game.Status != GameStatus.Playing)
        {
            return LegalActionsView.None;
        }

        if (game.IsStranded)
        {
            return LegalActionsView.None with { Wait = true };
        }

        var move = new List<Position>();
        var open = new List<Position>();
        var breaks = new List<Position>();
        var levers = new List<Position>();
        var chests = new List<Position>();
        var magus = game.Magus.Position;
        foreach (var target in Neighbours(magus))
        {
            if (game.IsContainer(target))
            {
                AddIfAccepted(game, new BreakAction(target), target, breaks);
            }
            else if (game.IsChest(target))
            {
                AddIfAccepted(game, new ChestAction(target), target, chests);
            }
            else if (game.Level.TerrainAt(target) == Terrain.Lever)
            {
                AddIfAccepted(game, new LeverAction(target), target, levers);
            }
            else if (game.GrilleAt(target) is { IsOpen: false })
            {
                AddIfAccepted(game, new OpenGrilleAction(target), target, open);
            }
            else
            {
                AddIfAccepted(game, new MoveAction(target), target, move);
            }
        }

        var activate = new List<Position>();
        foreach (var target in game.Activables())
        {
            AddIfAccepted(game, new ActivateAction(target), target, activate);
        }

        var canCast = game.Magus.Magic >= game.Rules.SpellCost;
        var teleports = canCast ? Sorted(game.TeleportTargets()) : Array.Empty<Position>();
        var swaps = canCast
            ? game.SwapTargets().ToArray()
            : Array.Empty<Position>();

        return new LegalActionsView(
            true,
            move,
            open,
            breaks,
            levers,
            activate,
            teleports,
            swaps.Length >= 2 ? Sorted(swaps) : Array.Empty<Position>(),
            chests);
    }

    public static bool IsAccepted(Game game, GameAction action)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(action);
        return game.Clone(new SeededRandom(0)).Apply(action).Success;
    }

    private static void AddIfAccepted(Game game, GameAction action, Position target, List<Position> into)
    {
        if (IsAccepted(game, action))
        {
            into.Add(target);
        }
    }

    private static IEnumerable<Position> Neighbours(Position origin) =>
        Position.Directions
            .Select(direction => origin + direction)
            .OrderBy(position => position.Y)
            .ThenBy(position => position.X);

    private static IReadOnlyList<Position> Sorted(IEnumerable<Position> positions) =>
        positions.OrderBy(position => position.Y).ThenBy(position => position.X).ToArray();
}
