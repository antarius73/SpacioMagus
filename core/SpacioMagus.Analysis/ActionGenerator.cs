using SpacioMagus.Core;

namespace SpacioMagus.Analysis;

public sealed record ActionFilter(bool AllowTeleport, bool AllowSwap)
{
    public static ActionFilter All { get; } = new(true, true);
}

public static class ActionGenerator
{
    public static IEnumerable<GameAction> Candidates(Game game, ActionFilter filter)
    {
        if (game.Status != GameStatus.Playing)
        {
            yield break;
        }

        yield return new WaitAction();
        foreach (var target in game.Activables())
        {
            yield return new ActivateAction(target);
        }

        var magus = game.Magus.Position;
        foreach (var direction in Position.Directions)
        {
            var target = magus + direction;
            if (game.IsContainer(target))
            {
                yield return new BreakAction(target);
                continue;
            }

            if (game.Level.LeverTarget(target) is { } herse)
            {
                if (game.GrilleAt(herse) is { IsOpen: false })
                {
                    yield return new LeverAction(target);
                }

                continue;
            }

            if (game.GrilleAt(target) is { IsOpen: false } && game.Level.IsDoor(target))
            {
                yield return new OpenGrilleAction(target);
                continue;
            }

            if (game.IsWalkable(target) && game.MonsterAt(target) is null && game.IsCornerFree(magus, target))
            {
                yield return new MoveAction(target);
            }
        }

        if (game.Magus.Magic < game.Rules.SpellCost)
        {
            yield break;
        }

        if (filter.AllowTeleport)
        {
            foreach (var target in game.TeleportTargets())
            {
                yield return new SpacioMagusAction(target);
            }
        }

        if (filter.AllowSwap)
        {
            var candidates = game.SwapCandidates();
            for (var first = 0; first < candidates.Count; first++)
            {
                for (var second = first + 1; second < candidates.Count; second++)
                {
                    yield return new SpacioCreaturaAction(candidates[first].Position, candidates[second].Position);
                }
            }
        }
    }

    public static string Describe(GameAction action) => action switch
    {
        MoveAction move => $"marcher {move.Target}",
        WaitAction => "attendre",
        OpenGrilleAction grille => $"ouvrir la porte {grille.Target}",
        BreakAction breaking => $"briser {breaking.Target}",
        LeverAction lever => $"manette {lever.Target}",
        ActivateAction activate => $"activer {activate.Target}",
        SpacioMagusAction teleport => $"Spacio Magus {teleport.Target}",
        SpacioCreaturaAction swap => $"Spacio Creatura {swap.First} ↔ {swap.Second}",
        _ => action.ToString() ?? "?",
    };
}
