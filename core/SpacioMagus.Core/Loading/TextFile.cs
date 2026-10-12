using System.Text;

namespace SpacioMagus.Core.Loading;

public static class TextFile
{
    public static IReadOnlyDictionary<string, string> Parse(string fileName, string content)
    {
        var issues = new List<LoadIssue>();
        var texts = new Dictionary<string, string>();
        string? key = null;
        var keyLine = 0;
        var buffer = new StringBuilder();
        var lines = (content ?? string.Empty).TrimStart('﻿').Replace("\r\n", "\n").Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                Store(fileName, texts, key, keyLine, buffer, issues);
                key = trimmed[1..^1].Trim();
                keyLine = index + 1;
                buffer.Clear();
                if (key.Length == 0)
                {
                    issues.Add(new LoadIssue(fileName, keyLine, 0, "Clé vide."));
                    key = null;
                }

                continue;
            }

            if (key is null)
            {
                if (trimmed.Length > 0 && !trimmed.StartsWith('#'))
                {
                    issues.Add(new LoadIssue(fileName, index + 1, 0, "Texte hors d'une clé [cle]."));
                }

                continue;
            }

            buffer.Append(line.TrimEnd()).Append('\n');
        }

        Store(fileName, texts, key, keyLine, buffer, issues);
        if (issues.Count > 0)
        {
            throw new LevelLoadException(issues);
        }

        return texts;
    }

    private static void Store(
        string fileName,
        Dictionary<string, string> texts,
        string? key,
        int keyLine,
        StringBuilder buffer,
        List<LoadIssue> issues)
    {
        if (key is null)
        {
            return;
        }

        if (!texts.TryAdd(key, buffer.ToString().Trim('\n')))
        {
            issues.Add(new LoadIssue(fileName, keyLine, 0, $"Clé [{key}] en double."));
        }
    }
}
