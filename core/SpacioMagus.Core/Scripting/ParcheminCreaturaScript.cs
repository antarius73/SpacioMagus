namespace SpacioMagus.Core.Scripting;

public sealed class ParcheminCreaturaScript : ILevelScript
{
    public string Id => "parchemin_creatura";

    public IEnumerable<string> Validate(LevelDefinition level)
    {
        var altar = level.CellsOf(Terrain.Altar).ToList();
        if (altar.Count == 0)
        {
            yield return $"Le script « {Id} » attend un autel dans la carte pour y poser le parchemin.";
        }
    }

    public void Setup(Game game, IRandomSource random)
    {
    }

    public IEnumerable<Position> Activables(Game game) => game.Level.CellsOf(Terrain.Altar);

    public ActionError CanActivate(Game game, Position target)
    {
        if (game.Level.TerrainAt(target) != Terrain.Altar)
        {
            return ActionError.NotActivable;
        }

        if (game.CanSwapObjects)
        {
            return ActionError.AlreadyActive;
        }

        return game.Magus.Position.IsAdjacentTo(target) ? ActionError.None : ActionError.NotAdjacent;
    }

    public void Activate(Game game, Position target)
    {
        foreach (var cell in game.Level.CellsOf(Terrain.Altar))
        {
            game.MarkActivated(cell);
        }

        game.EnableObjectSwap();
        game.Emit(new ScrollTaken(target));
    }
}
