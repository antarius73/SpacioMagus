namespace SpacioMagus.Core;

public sealed record RuleSet
{
    public static RuleSet Default { get; } = new();

    public int MagicMax { get; init; } = 4;

    public int SpellCost { get; init; } = 1;

    public int RegenPerTurn { get; init; } = 1;

    public int SpellRange { get; init; } = 8;

    public int StunDuration { get; init; } = 4;

    public int GrilleOpenDuration { get; init; } = 5;

    public void Validate()
    {
        Require(MagicMax >= 1, nameof(MagicMax), MagicMax);
        Require(SpellCost >= 0, nameof(SpellCost), SpellCost);
        Require(RegenPerTurn >= 0, nameof(RegenPerTurn), RegenPerTurn);
        Require(SpellRange >= 1, nameof(SpellRange), SpellRange);
        Require(StunDuration >= 1, nameof(StunDuration), StunDuration);
        Require(GrilleOpenDuration >= 1, nameof(GrilleOpenDuration), GrilleOpenDuration);
    }

    private static void Require(bool condition, string name, int value)
    {
        if (!condition)
        {
            throw new ArgumentOutOfRangeException(name, value, $"Valeur invalide pour {name} : {value}.");
        }
    }
}
