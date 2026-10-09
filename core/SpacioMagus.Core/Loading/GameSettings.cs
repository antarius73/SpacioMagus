namespace SpacioMagus.Core.Loading;

public sealed record GameSettings(RuleSet Rules)
{
    public static GameSettings Default { get; } = new(RuleSet.Default);

    public static GameSettings Parse(string fileName, string content)
    {
        var issues = new List<LoadIssue>();
        var document = SectionedText.Parse(fileName, content, issues, new[] { "regles" }, "regles");
        var rules = RuleSet.Default;
        foreach (var (line, rawKey, value) in SectionedText.KeyValues(fileName, document.Lines("regles"), issues))
        {
            var key = rawKey.ToLowerInvariant();
            if (RuleKeys.IsRuleKey(key))
            {
                rules = RuleKeys.Apply(rules, fileName, line, key, value, issues);
            }
            else
            {
                issues.Add(new LoadIssue(fileName, line.Number, 0, $"Clé inconnue « {key} ». Clés possibles : {string.Join(", ", RuleKeys.Names)}."));
            }
        }

        RuleKeys.Validate(rules, fileName, issues);
        if (issues.Count > 0)
        {
            throw new LevelLoadException(issues);
        }

        return new GameSettings(rules);
    }
}
