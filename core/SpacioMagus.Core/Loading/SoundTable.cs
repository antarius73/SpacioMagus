using System.Globalization;

namespace SpacioMagus.Core.Loading;

public sealed record SoundEntry(IReadOnlyList<string> Files, float VolumeDb);

public static class SoundTable
{
    public const float MinVolumeDb = -40f;
    public const float MaxVolumeDb = 24f;

    public static readonly IReadOnlyList<string> Keys = new[]
    {
        "ui.tap", "ui.back", "ui.confirm", "ui.locked", "ui.popup_open", "ui.popup_close", "ui.toggle", "ui.title", "ui.star", "ui.record",
        "mg.step", "mg.wait", "mg.invalid", "mg.nomagic",
        "sp.magus_out", "sp.magus_in", "sp.select", "sp.creatura",
        "it.break", "it.reveal", "it.drink",
        "mo.spot", "mo.lost", "mo.fear", "mo.block", "mo.mut_on", "mo.mut_off", "mo.stun", "mo.recover",
        "dr.open", "dr.close", "dr.delayed",
        "gr.lever", "gr.open", "gr.tick", "gr.last", "gr.close", "gr.push", "gr.delayed",
        "dc.torch", "dc.brazier",
        "end.caught", "end.lose", "end.win",
        "mu.title", "mu.final",
    };

    public static IReadOnlyDictionary<string, SoundEntry> Parse(string fileName, string content)
    {
        var issues = new List<LoadIssue>();
        var document = SectionedText.Parse(fileName, content, issues, new[] { "sons" }, "sons");
        var sounds = new Dictionary<string, SoundEntry>();
        foreach (var (line, rawKey, value) in SectionedText.KeyValues(fileName, document.Lines("sons"), issues))
        {
            var key = rawKey.ToLowerInvariant();
            if (!Keys.Contains(key))
            {
                issues.Add(new LoadIssue(fileName, line.Number, 0, $"Son inconnu « {key} ». Sons possibles : {string.Join(", ", Keys)}."));
                continue;
            }

            if (sounds.ContainsKey(key))
            {
                issues.Add(new LoadIssue(fileName, line.Number, 0, $"Son « {key} » défini deux fois."));
                continue;
            }

            if (ParseEntry(fileName, line.Number, key, value, issues) is { } entry)
            {
                sounds[key] = entry;
            }
        }

        if (issues.Count > 0)
        {
            throw new LevelLoadException(issues);
        }

        return sounds;
    }

    private static SoundEntry? ParseEntry(string fileName, int lineNumber, string key, string value, List<LoadIssue> issues)
    {
        var parts = value.Split(',');
        if (parts.Length > 2)
        {
            issues.Add(new LoadIssue(fileName, lineNumber, 0, $"« {key} » : format attendu « fichier [fichier…], volume_db »."));
            return null;
        }

        var files = parts[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (files.Length == 0)
        {
            issues.Add(new LoadIssue(fileName, lineNumber, 0, $"« {key} » : aucun fichier indiqué."));
            return null;
        }

        var volume = 0f;
        if (parts.Length == 2)
        {
            var text = parts[1].Trim();
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out volume))
            {
                issues.Add(new LoadIssue(fileName, lineNumber, 0, $"« {key} » : volume « {text} » invalide, nombre en dB attendu (ex. -3 ou 2.5)."));
                return null;
            }

            if (volume < MinVolumeDb || volume > MaxVolumeDb)
            {
                issues.Add(new LoadIssue(fileName, lineNumber, 0, $"« {key} » : volume {volume.ToString(CultureInfo.InvariantCulture)} dB hors limites ({MinVolumeDb} à {MaxVolumeDb})."));
                return null;
            }
        }

        return new SoundEntry(files, volume);
    }
}
