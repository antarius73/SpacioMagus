namespace SpacioMagus.Core.Saving;

public sealed class GameJournal
{
    private readonly List<GameAction> _actions = new();

    public GameJournal(LevelDefinition level, RuleSet rules, ulong seed)
    {
        Seed = seed;
        Game = new Game(level, rules, new SeededRandom(seed));
    }

    public Game Game { get; }

    public ulong Seed { get; }

    public IReadOnlyList<GameAction> Actions => _actions;

    public ActionResult Play(GameAction action)
    {
        var result = Game.Apply(action);
        if (result.Success)
        {
            _actions.Add(action);
        }

        return result;
    }
}
