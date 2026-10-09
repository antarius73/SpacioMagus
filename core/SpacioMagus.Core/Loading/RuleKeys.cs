namespace SpacioMagus.Core.Loading;

internal static class RuleKeys
{
    private static readonly Dictionary<string, Func<RuleSet, int, RuleSet>> Setters = new()
    {
        ["magie_max"] = (rules, value) => rules with { MagicMax = value },
        ["cout_sort"] = (rules, value) => rules with { SpellCost = value },
        ["regen_par_tour"] = (rules, value) => rules with { RegenPerTurn = value },
        ["portee_sort"] = (rules, value) => rules with { SpellRange = value },
        ["duree_etourdi"] = (rules, value) => rules with { StunDuration = value },
        ["duree_grille"] = (rules, value) => rules with { GrilleOpenDuration = value },
    };

    public static IEnumerable<string> Names => Setters.Keys;

    public static bool IsRuleKey(string key) => Setters.ContainsKey(key);

    public static RuleSet Apply(RuleSet rules, string fileName, SourceLine line, string key, string value, List<LoadIssue> issues)
    {
        if (!int.TryParse(value, out var number))
        {
            issues.Add(new LoadIssue(fileName, line.Number, 0, $"La valeur de « {key} » doit être un nombre entier : « {value} »."));
            return rules;
        }

        return Setters[key](rules, number);
    }

    public static void Validate(RuleSet rules, string fileName, List<LoadIssue> issues)
    {
        try
        {
            rules.Validate();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            issues.Add(new LoadIssue(fileName, 0, 0, exception.Message));
        }
    }
}
