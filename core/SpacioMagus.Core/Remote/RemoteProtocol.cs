using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using SpacioMagus.Core.Loading;
using SpacioMagus.Core.Saving;

namespace SpacioMagus.Core.Remote;

public static class RemoteCommands
{
    public const string State = "state";
    public const string Levels = "levels";
    public const string Start = "start";
    public const string Resume = "resume";
    public const string Play = "play";
    public const string Ui = "ui";

    public static IReadOnlyList<string> All { get; } = new[] { State, Levels, Start, Resume, Play, Ui };
}

public static class RemoteErrors
{
    public const string InvalidRequest = "requete_invalide";
    public const string UnknownCommand = "commande_inconnue";
    public const string NoGame = "pas_de_partie";
    public const string StaleTurn = "tour_perime";
    public const string ScreenOpen = "ecran_ouvert";
    public const string Busy = "occupe";
    public const string UnknownLevel = "niveau_inconnu";
    public const string LockedLevel = "niveau_verrouille";
    public const string NothingToResume = "rien_a_reprendre";
    public const string Refused = "action_refusee";
    public const string ControlNotFound = "controle_introuvable";
    public const string ControlDisabled = "controle_desactive";
    public const string InvalidValue = "valeur_invalide";
    public const string Internal = "erreur_interne";
}

public enum RemoteScreen
{
    Title,
    LevelSelect,
    Story,
    Playing,
    Paused,
    Victory,
    Defeat,
    Other,
}

public sealed record RemoteRequest(
    long Id,
    string Command,
    int? Turn = null,
    GameAction? Action = null,
    string? Level = null,
    string? Control = null,
    int? Value = null);

public enum UiKind
{
    Button,
    Choice,
    Slider,
    Door,
    Close,
}

public sealed record UiControl(
    string Id,
    UiKind Kind,
    string Label,
    bool Enabled,
    IReadOnlyList<string>? Options = null,
    int? Value = null,
    int? Min = null,
    int? Max = null);

public sealed record UiView(IReadOnlyList<string> Texts, IReadOnlyList<UiControl> Controls)
{
    public static UiView Empty { get; } = new(Array.Empty<string>(), Array.Empty<UiControl>());
}

public sealed record RemoteLevel(string File, string Name, bool Unlocked, int? Stars, int? BestTurns);

public sealed record RemoteResponse(
    long Id,
    bool Ok,
    RemoteScreen Screen,
    string? Error = null,
    string? Message = null,
    ActionError? Refusal = null,
    PlayerView? State = null,
    IReadOnlyList<string>? Events = null,
    IReadOnlyList<RemoteLevel>? Levels = null,
    UiView? Ui = null)
{
    public static RemoteResponse Success(long id, RemoteScreen screen, PlayerView? state = null, IReadOnlyList<string>? events = null) =>
        new(id, true, screen, State: state, Events: events);

    public static RemoteResponse Failure(long id, RemoteScreen screen, string error, string message, PlayerView? state = null) =>
        new(id, false, screen, error, message, State: state);

    public static RemoteResponse Refused(long id, RemoteScreen screen, ActionError refusal, PlayerView? state) =>
        new(id, false, screen, RemoteErrors.Refused, $"Action refusée par le jeu : {refusal}.", refusal, state);
}

public sealed class RemoteProtocolException : Exception
{
    public RemoteProtocolException(string error, string message, long id = 0)
        : base(message)
    {
        Error = error;
        Id = id;
    }

    public string Error { get; }

    public long Id { get; }
}

public static class RemoteProtocol
{
    public const int Version = 1;

    private const string IdKey = "id";
    private const string CommandKey = "cmd";
    private const string TurnKey = "turn";
    private const string ActionKey = "action";
    private const string LevelKey = "level";
    private const string ControlKey = "control";
    private const string ValueKey = "value";

    public static JsonSerializerOptions JsonOptions { get; } = CreateOptions();

    public static string WriteRequest(RemoteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            writer.WriteNumber(IdKey, request.Id);
            writer.WriteString(CommandKey, request.Command);
            if (request.Turn is { } turn)
            {
                writer.WriteNumber(TurnKey, turn);
            }

            if (request.Action is { } action)
            {
                writer.WritePropertyName(ActionKey);
                SaveCodec.WriteAction(writer, action);
            }

            if (request.Level is { } level)
            {
                writer.WriteString(LevelKey, level);
            }

            if (request.Control is { } control)
            {
                writer.WriteString(ControlKey, control);
            }

            if (request.Value is { } value)
            {
                writer.WriteNumber(ValueKey, value);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static RemoteRequest ReadRequest(string line)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line ?? string.Empty);
        }
        catch (JsonException exception)
        {
            throw new RemoteProtocolException(RemoteErrors.InvalidRequest, $"JSON invalide : {exception.Message}");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new RemoteProtocolException(RemoteErrors.InvalidRequest, "La requête doit être un objet JSON.");
            }

            if (!root.TryGetProperty(IdKey, out var idElement) || idElement.ValueKind != JsonValueKind.Number || !idElement.TryGetInt64(out var id))
            {
                throw new RemoteProtocolException(RemoteErrors.InvalidRequest, $"« {IdKey} » entier obligatoire.");
            }

            if (!root.TryGetProperty(CommandKey, out var commandElement) || commandElement.ValueKind != JsonValueKind.String)
            {
                throw new RemoteProtocolException(RemoteErrors.InvalidRequest, $"« {CommandKey} » texte obligatoire.", id);
            }

            var command = commandElement.GetString()!;
            if (!RemoteCommands.All.Contains(command))
            {
                throw new RemoteProtocolException(
                    RemoteErrors.UnknownCommand,
                    $"Commande « {command} » inconnue. Commandes : {string.Join(", ", RemoteCommands.All)}.",
                    id);
            }

            int? turn = null;
            if (root.TryGetProperty(TurnKey, out var turnElement))
            {
                if (turnElement.ValueKind != JsonValueKind.Number || !turnElement.TryGetInt32(out var value))
                {
                    throw new RemoteProtocolException(RemoteErrors.InvalidRequest, $"« {TurnKey} » doit être un entier.", id);
                }

                turn = value;
            }

            GameAction? action = null;
            if (root.TryGetProperty(ActionKey, out var actionElement))
            {
                try
                {
                    action = SaveCodec.ReadAction(actionElement, "requête", 1);
                }
                catch (LevelLoadException exception)
                {
                    throw new RemoteProtocolException(RemoteErrors.InvalidRequest, exception.Message, id);
                }
            }

            string? level = null;
            if (root.TryGetProperty(LevelKey, out var levelElement))
            {
                if (levelElement.ValueKind != JsonValueKind.String)
                {
                    throw new RemoteProtocolException(RemoteErrors.InvalidRequest, $"« {LevelKey} » doit être un texte.", id);
                }

                level = levelElement.GetString();
            }

            string? control = null;
            if (root.TryGetProperty(ControlKey, out var controlElement))
            {
                if (controlElement.ValueKind != JsonValueKind.String)
                {
                    throw new RemoteProtocolException(RemoteErrors.InvalidRequest, $"« {ControlKey} » doit être un texte.", id);
                }

                control = controlElement.GetString();
            }

            int? number = null;
            if (root.TryGetProperty(ValueKey, out var valueElement))
            {
                if (valueElement.ValueKind != JsonValueKind.Number || !valueElement.TryGetInt32(out var parsed))
                {
                    throw new RemoteProtocolException(RemoteErrors.InvalidRequest, $"« {ValueKey} » doit être un entier.", id);
                }

                number = parsed;
            }

            if (command == RemoteCommands.Ui && string.IsNullOrWhiteSpace(control))
            {
                throw new RemoteProtocolException(RemoteErrors.InvalidRequest, $"La commande « {RemoteCommands.Ui} » exige « {ControlKey} ».", id);
            }

            if (command == RemoteCommands.Play && action is null)
            {
                throw new RemoteProtocolException(RemoteErrors.InvalidRequest, $"La commande « {RemoteCommands.Play} » exige « {ActionKey} ».", id);
            }

            if (command == RemoteCommands.Start && string.IsNullOrWhiteSpace(level))
            {
                throw new RemoteProtocolException(RemoteErrors.InvalidRequest, $"La commande « {RemoteCommands.Start} » exige « {LevelKey} ».", id);
            }

            return new RemoteRequest(id, command, turn, action, level, control, number);
        }
    }

    public static string WriteResponse(RemoteResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return JsonSerializer.Serialize(response, JsonOptions);
    }

    public static RemoteResponse ReadResponse(string line)
    {
        try
        {
            return JsonSerializer.Deserialize<RemoteResponse>(line ?? string.Empty, JsonOptions)
                ?? throw new RemoteProtocolException(RemoteErrors.InvalidRequest, "Réponse vide.");
        }
        catch (JsonException exception)
        {
            throw new RemoteProtocolException(RemoteErrors.InvalidRequest, $"Réponse illisible : {exception.Message}");
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new PositionJsonConverter());
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}

public sealed class PositionJsonConverter : JsonConverter<Position>
{
    public override Position Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Une position s'écrit [x, y].");
        }

        reader.Read();
        var x = reader.GetInt32();
        reader.Read();
        var y = reader.GetInt32();
        reader.Read();
        if (reader.TokenType != JsonTokenType.EndArray)
        {
            throw new JsonException("Une position s'écrit [x, y].");
        }

        return new Position(x, y);
    }

    public override void Write(Utf8JsonWriter writer, Position value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteEndArray();
    }
}
