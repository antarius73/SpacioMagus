using System.Text;
using System.Text.Json;
using SpacioMagus.Core.Loading;

namespace SpacioMagus.Core.Progress;

public static class RecordCodec
{
    public const int FormatVersion = 1;

    private const string FormatKey = "format";
    private const string LevelsKey = "niveaux";
    private const string TurnsKey = "tours";
    private const string StarsKey = "etoiles";
    private const string FewestTeleportsKey = "spacio_magus_min";
    private const string MostTeleportsKey = "spacio_magus_max";
    private const string FewestSwapsKey = "spacio_creatura_min";
    private const string MostSwapsKey = "spacio_creatura_max";

    public static string Serialize(RecordBook book)
    {
        ArgumentNullException.ThrowIfNull(book);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber(FormatKey, FormatVersion);
            writer.WriteStartObject(LevelsKey);
            foreach (var (level, record) in book.Levels.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                writer.WriteStartObject(level);
                writer.WriteNumber(TurnsKey, record.FewestTurns);
                writer.WriteNumber(StarsKey, record.MostStars);
                writer.WriteNumber(FewestTeleportsKey, record.FewestTeleports);
                writer.WriteNumber(MostTeleportsKey, record.MostTeleports);
                writer.WriteNumber(FewestSwapsKey, record.FewestSwaps);
                writer.WriteNumber(MostSwapsKey, record.MostSwaps);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static RecordBook Parse(string fileName, string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content ?? string.Empty);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw Fail(fileName, "Le contenu doit être un objet JSON.");
            }

            var format = Integer(root, FormatKey, fileName, 0);
            if (format != FormatVersion)
            {
                throw Fail(fileName, $"Version de format inconnue : {format}.");
            }

            if (!root.TryGetProperty(LevelsKey, out var levels) || levels.ValueKind != JsonValueKind.Object)
            {
                throw Fail(fileName, $"« {LevelsKey} » doit être un objet.");
            }

            var records = new Dictionary<string, LevelRecord>();
            foreach (var level in levels.EnumerateObject())
            {
                if (level.Value.ValueKind != JsonValueKind.Object)
                {
                    throw Fail(fileName, $"Niveau « {level.Name} » : un objet est attendu.");
                }

                var context = $"{fileName} ({level.Name})";
                var stars = Integer(level.Value, StarsKey, context, 1);
                if (stars > StarThresholds.MaxStars)
                {
                    throw Fail(context, $"« {StarsKey} » doit être compris entre 1 et {StarThresholds.MaxStars}.");
                }

                records[level.Name] = new LevelRecord(
                    Integer(level.Value, TurnsKey, context, 1),
                    stars,
                    Integer(level.Value, FewestTeleportsKey, context, 0),
                    Integer(level.Value, MostTeleportsKey, context, 0),
                    Integer(level.Value, FewestSwapsKey, context, 0),
                    Integer(level.Value, MostSwapsKey, context, 0));
            }

            return new RecordBook(records);
        }
        catch (JsonException exception)
        {
            throw Fail(fileName, $"JSON invalide : {exception.Message}");
        }
    }

    private static int Integer(JsonElement element, string key, string context, int minimum)
    {
        if (!element.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
        {
            throw Fail(context, $"« {key} » doit être un nombre entier.");
        }

        if (number < minimum)
        {
            throw Fail(context, $"« {key} » doit valoir au moins {minimum} : {number}.");
        }

        return number;
    }

    private static LevelLoadException Fail(string context, string message) =>
        new(new[] { new LoadIssue(context, 0, 0, message) });
}
