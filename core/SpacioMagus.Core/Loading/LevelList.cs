namespace SpacioMagus.Core.Loading;

public static class LevelList
{
    public static IReadOnlyList<string> Parse(string fileName, string content)
    {
        var issues = new List<LoadIssue>();
        var document = SectionedText.Parse(fileName, content, issues, new[] { "niveaux" }, "niveaux");
        var levels = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in document.Lines("niveaux"))
        {
            var name = line.Text.Trim();
            if (name.Length == 0)
            {
                continue;
            }

            if (!seen.Add(name))
            {
                issues.Add(new LoadIssue(fileName, line.Number, 0, $"Le niveau « {name} » est listé deux fois."));
                continue;
            }

            levels.Add(name);
        }

        if (levels.Count == 0)
        {
            issues.Add(new LoadIssue(fileName, 0, 0, "La liste des niveaux est vide."));
        }

        if (issues.Count > 0)
        {
            throw new LevelLoadException(issues);
        }

        return levels;
    }
}
