namespace SpacioMagus.Core.Loading;

internal sealed record SourceLine(int Number, string Text);

internal sealed class SectionedText
{
    private readonly Dictionary<string, List<SourceLine>> _sections = new();
    private readonly Dictionary<string, int> _headers = new();

    private SectionedText()
    {
    }

    public static SectionedText Parse(
        string fileName,
        string content,
        List<LoadIssue> issues,
        IReadOnlyCollection<string> allowedSections,
        string? defaultSection)
    {
        var result = new SectionedText();
        var current = defaultSection;
        if (current is not null)
        {
            result._sections[current] = new List<SourceLine>();
        }

        var lines = (content ?? string.Empty).TrimStart('﻿').Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var number = index + 1;
            var text = lines[index].TrimEnd('\r').TrimEnd();
            var trimmed = text.Trim();
            if (trimmed.StartsWith(';'))
            {
                continue;
            }

            if (trimmed.Length > 2 && trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                var name = trimmed[1..^1].Trim().ToLowerInvariant();
                if (!allowedSections.Contains(name))
                {
                    issues.Add(new LoadIssue(fileName, number, 1, $"Section [{name}] inconnue. Sections possibles : {string.Join(", ", allowedSections.Select(s => $"[{s}]"))}."));
                    current = null;
                    continue;
                }

                if (result._headers.ContainsKey(name))
                {
                    issues.Add(new LoadIssue(fileName, number, 1, $"Section [{name}] présente deux fois."));
                    current = null;
                    continue;
                }

                result._headers[name] = number;
                result._sections.TryAdd(name, new List<SourceLine>());
                current = name;
                continue;
            }

            if (current is null)
            {
                if (trimmed.Length > 0)
                {
                    issues.Add(new LoadIssue(fileName, number, 1, "Ligne en dehors de toute section."));
                }

                continue;
            }

            result._sections[current].Add(new SourceLine(number, text));
        }

        return result;
    }

    public bool Has(string section) => _headers.ContainsKey(section);

    public int HeaderLine(string section) => _headers.TryGetValue(section, out var line) ? line : 0;

    public IReadOnlyList<SourceLine> Lines(string section) =>
        _sections.TryGetValue(section, out var lines) ? lines : Array.Empty<SourceLine>();

    public static IEnumerable<(SourceLine Line, string Key, string Value)> KeyValues(
        string fileName,
        IEnumerable<SourceLine> lines,
        List<LoadIssue> issues)
    {
        foreach (var line in lines)
        {
            if (line.Text.Trim().Length == 0)
            {
                continue;
            }

            var separator = line.Text.IndexOf('=');
            if (separator <= 0)
            {
                issues.Add(new LoadIssue(fileName, line.Number, 1, "Ligne attendue au format « clé = valeur »."));
                continue;
            }

            var key = line.Text[..separator].Trim();
            var value = line.Text[(separator + 1)..].Trim();
            yield return (line, key, value);
        }
    }
}
