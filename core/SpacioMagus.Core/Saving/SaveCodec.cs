using System.Text;
using System.Text.Json;
using SpacioMagus.Core.Loading;

namespace SpacioMagus.Core.Saving;

public static class SaveCodec
{
    public const int FormatVersion = 1;

    private const string FormatKey = "format";
    private const string RulesKey = "regles";
    private const string LevelKey = "niveau";
    private const string FingerprintKey = "empreinte";
    private const string SeedKey = "graine";
    private const string ChecksumKey = "controle";
    private const string ActionsKey = "actions";
    private const string TypeKey = "type";
    private const string MoveType = "marcher";
    private const string WaitType = "attendre";
    private const string GrilleType = "porte";
    private const string SpacioMagusType = "spacio_magus";
    private const string SpacioCreaturaType = "spacio_creatura";
    private const string BreakType = "briser";

    public static string Serialize(SaveGame save)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber(FormatKey, FormatVersion);
            writer.WriteNumber(RulesKey, save.RulesVersion);
            writer.WriteString(LevelKey, save.LevelFile);
            WriteOptional(writer, FingerprintKey, save.Fingerprint);
            writer.WriteNumber(SeedKey, save.Seed);
            WriteOptional(writer, ChecksumKey, save.Checksum);
            writer.WriteStartArray(ActionsKey);
            foreach (var action in save.Actions)
            {
                WriteAction(writer, action);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static SaveGame Parse(string fileName, string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content ?? string.Empty);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw Fail(fileName, "Le contenu doit être un objet JSON.");
            }

            var format = RequiredInt(root, FormatKey, fileName);
            if (format != FormatVersion)
            {
                throw Fail(fileName, $"Version de format inconnue : {format}.");
            }

            var actions = new List<GameAction>();
            var array = Required(root, ActionsKey, fileName);
            if (array.ValueKind != JsonValueKind.Array)
            {
                throw Fail(fileName, $"« {ActionsKey} » doit être une liste.");
            }

            var index = 0;
            foreach (var element in array.EnumerateArray())
            {
                index++;
                actions.Add(ReadAction(element, fileName, index));
            }

            var levelFile = RequiredString(root, LevelKey, fileName);
            if (levelFile.Length == 0)
            {
                throw Fail(fileName, $"« {LevelKey} » est vide.");
            }

            var seedElement = Required(root, SeedKey, fileName);
            if (seedElement.ValueKind != JsonValueKind.Number || !seedElement.TryGetUInt64(out var seed))
            {
                throw Fail(fileName, $"« {SeedKey} » doit être un entier positif.");
            }

            return new SaveGame(
                RequiredInt(root, RulesKey, fileName),
                levelFile,
                OptionalString(root, FingerprintKey, fileName),
                seed,
                actions,
                OptionalString(root, ChecksumKey, fileName));
        }
        catch (JsonException exception)
        {
            throw Fail(fileName, $"JSON invalide : {exception.Message}");
        }
    }

    private static void WriteAction(Utf8JsonWriter writer, GameAction action)
    {
        writer.WriteStartObject();
        switch (action)
        {
            case MoveAction move:
                writer.WriteString(TypeKey, MoveType);
                WritePosition(writer, move.Target, "x", "y");
                break;
            case WaitAction:
                writer.WriteString(TypeKey, WaitType);
                break;
            case OpenGrilleAction grille:
                writer.WriteString(TypeKey, GrilleType);
                WritePosition(writer, grille.Target, "x", "y");
                break;
            case SpacioMagusAction teleport:
                writer.WriteString(TypeKey, SpacioMagusType);
                WritePosition(writer, teleport.Target, "x", "y");
                break;
            case SpacioCreaturaAction swap:
                writer.WriteString(TypeKey, SpacioCreaturaType);
                WritePosition(writer, swap.First, "x", "y");
                WritePosition(writer, swap.Second, "x2", "y2");
                break;
            case BreakAction breaking:
                writer.WriteString(TypeKey, BreakType);
                WritePosition(writer, breaking.Target, "x", "y");
                break;
            default:
                throw new ArgumentException($"Action non sauvegardable : {action.GetType().Name}.", nameof(action));
        }

        writer.WriteEndObject();
    }

    private static GameAction ReadAction(JsonElement element, string fileName, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw Fail(fileName, $"Action {index} : un objet JSON est attendu.");
        }

        var context = $"{fileName} (action {index})";
        var type = RequiredString(element, TypeKey, context);
        return type switch
        {
            MoveType => new MoveAction(ReadPosition(element, "x", "y", context)),
            WaitType => new WaitAction(),
            GrilleType => new OpenGrilleAction(ReadPosition(element, "x", "y", context)),
            SpacioMagusType => new SpacioMagusAction(ReadPosition(element, "x", "y", context)),
            SpacioCreaturaType => new SpacioCreaturaAction(
                ReadPosition(element, "x", "y", context),
                ReadPosition(element, "x2", "y2", context)),
            BreakType => new BreakAction(ReadPosition(element, "x", "y", context)),
            _ => throw Fail(fileName, $"Action {index} : type inconnu « {type} »."),
        };
    }

    private static void WritePosition(Utf8JsonWriter writer, Position position, string xKey, string yKey)
    {
        writer.WriteNumber(xKey, position.X);
        writer.WriteNumber(yKey, position.Y);
    }

    private static Position ReadPosition(JsonElement element, string xKey, string yKey, string context) =>
        new(RequiredInt(element, xKey, context), RequiredInt(element, yKey, context));

    private static void WriteOptional(Utf8JsonWriter writer, string key, string? value)
    {
        if (value is null)
        {
            writer.WriteNull(key);
        }
        else
        {
            writer.WriteString(key, value);
        }
    }

    private static JsonElement Required(JsonElement element, string key, string context)
    {
        if (!element.TryGetProperty(key, out var value))
        {
            throw Fail(context, $"Champ « {key} » absent.");
        }

        return value;
    }

    private static int RequiredInt(JsonElement element, string key, string context)
    {
        var value = Required(element, key, context);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
        {
            throw Fail(context, $"« {key} » doit être un nombre entier.");
        }

        return number;
    }

    private static string RequiredString(JsonElement element, string key, string context)
    {
        var value = Required(element, key, context);
        if (value.ValueKind != JsonValueKind.String)
        {
            throw Fail(context, $"« {key} » doit être un texte.");
        }

        return value.GetString()!;
    }

    private static string? OptionalString(JsonElement element, string key, string context)
    {
        if (!element.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw Fail(context, $"« {key} » doit être un texte.");
        }

        return value.GetString();
    }

    private static LevelLoadException Fail(string context, string message) =>
        new(new[] { new LoadIssue(context, 0, 0, message) });
}
