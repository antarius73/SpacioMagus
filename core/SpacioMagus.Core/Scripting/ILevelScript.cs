namespace SpacioMagus.Core.Scripting;

public interface ILevelScript
{
    string Id { get; }

    IEnumerable<string> Validate(LevelDefinition level);

    void Setup(Game game, IRandomSource random);

    IEnumerable<Position> Activables(Game game);

    ActionError CanActivate(Game game, Position target);

    void Activate(Game game, Position target);

    IEnumerable<Position> LeverGrilles(LevelDefinition level, Position lever) =>
        level.LeverTarget(lever) is { } grille ? new[] { grille } : Array.Empty<Position>();
}
