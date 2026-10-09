using System.Diagnostics.CodeAnalysis;

namespace SpacioMagus.Core.Loading;

public enum LegendLayer
{
    Map,
    Decor,
}

public sealed record LegendEntry(char Symbol, string Id, LegendLayer Layer);

public sealed class Legend
{
    private readonly Dictionary<char, LegendEntry> _entries;

    private Legend(Dictionary<char, LegendEntry> entries)
    {
        _entries = entries;
    }

    public IReadOnlyCollection<LegendEntry> Entries => _entries.Values;

    public bool TryGet(char symbol, [MaybeNullWhen(false)] out LegendEntry entry) => _entries.TryGetValue(symbol, out entry);

    public static Legend Parse(string fileName, string content)
    {
        var issues = new List<LoadIssue>();
        var document = SectionedText.Parse(fileName, content, issues, new[] { "legende" }, "legende");
        var entries = new Dictionary<char, LegendEntry>();
        foreach (var (line, key, value) in SectionedText.KeyValues(fileName, document.Lines("legende"), issues))
        {
            if (key.Length != 1)
            {
                issues.Add(new LoadIssue(fileName, line.Number, 0, $"Le caractère doit être unique : « {key} »."));
                continue;
            }

            var symbol = key[0];
            if (char.IsDigit(symbol) || char.IsWhiteSpace(symbol) || symbol is ';' or '[')
            {
                issues.Add(new LoadIssue(fileName, line.Number, 0, $"Le caractère « {symbol} » est réservé (chiffres, espace, « ; » et « [ »)."));
                continue;
            }

            var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length is 0 or > 2)
            {
                issues.Add(new LoadIssue(fileName, line.Number, 0, "Format attendu : « X = élément » ou « X = élément, decor »."));
                continue;
            }

            var id = parts[0].ToLowerInvariant();
            var layer = LegendLayer.Map;
            if (parts.Length == 2)
            {
                if (!parts[1].Equals("decor", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new LoadIssue(fileName, line.Number, 0, $"Couche inconnue « {parts[1]} ». Seule « decor » est possible."));
                    continue;
                }

                layer = LegendLayer.Decor;
            }

            if (layer == LegendLayer.Map && !ElementIds.MapElements.Contains(id))
            {
                issues.Add(new LoadIssue(fileName, line.Number, 0, $"Élément inconnu « {id} ». Éléments possibles : {string.Join(", ", ElementIds.MapElements)}."));
                continue;
            }

            if (!entries.TryAdd(symbol, new LegendEntry(symbol, id, layer)))
            {
                issues.Add(new LoadIssue(fileName, line.Number, 0, $"Le caractère « {symbol} » est défini deux fois."));
            }
        }

        if (issues.Count > 0)
        {
            throw new LevelLoadException(issues);
        }

        return new Legend(entries);
    }
}
