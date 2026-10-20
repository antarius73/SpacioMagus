using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SpacioMagus.Core;
using SpacioMagus.Core.Remote;

namespace SpacioMagus.Mcp;

[McpServerToolType]
public sealed class GameTools
{
    private static readonly IReadOnlyDictionary<string, Position> Directions = new Dictionary<string, Position>(StringComparer.OrdinalIgnoreCase)
    {
        ["N"] = new(0, -1),
        ["NE"] = new(1, -1),
        ["E"] = new(1, 0),
        ["SE"] = new(1, 1),
        ["S"] = new(0, 1),
        ["SO"] = new(-1, 1),
        ["SW"] = new(-1, 1),
        ["O"] = new(-1, 0),
        ["W"] = new(-1, 0),
        ["NO"] = new(-1, -1),
        ["NW"] = new(-1, -1),
    };

    private readonly GameConnection _connection;
    private readonly GameLauncher _launcher;
    private readonly McpSettings _settings;

    public GameTools(GameConnection connection, GameLauncher launcher, McpSettings settings)
    {
        _connection = connection;
        _launcher = launcher;
        _settings = settings;
    }

    [McpServerTool(Name = "lancer_jeu"), Description(
        "Compile puis lance le jeu Godot en mode pilotable (si ce n'est pas déjà fait) et renvoie l'écran affiché. " +
        "À appeler en premier. Le joueur humain voit la partie et garde la souris.")]
    public async Task<CallToolResult> LaunchGame(
        [Description("Compiler le projet C# avant le lancement (recommandé après une modification du code).")] bool compiler = true,
        CancellationToken cancellationToken = default)
    {
        var report = await _launcher.LaunchAsync(compiler && _settings.Build, cancellationToken);
        var response = await _connection.SendAsync(id => new RemoteRequest(id, RemoteCommands.State), cancellationToken);
        return Result(response, report);
    }

    [McpServerTool(Name = "etat", ReadOnly = true), Description(
        "Écran actuel, contrôles cliquables et, en partie, l'état complet vu par le joueur : carte avec axes, créatures, portes, fioles, " +
        "zones de peur et liste exacte des actions possibles ce tour.")]
    public async Task<CallToolResult> State(CancellationToken cancellationToken = default) =>
        Result(await _connection.SendAsync(id => new RemoteRequest(id, RemoteCommands.State), cancellationToken));

    [McpServerTool(Name = "jouer"), Description(
        "Joue une action du magus pendant la partie (un tour). Le coup part après un délai « humain » et la réponse arrive après les animations, " +
        "avec les événements du tour et le nouvel état. Actions : attendre ; deplacer (x,y voisine ou direction N/NE/E/SE/S/SO/O/NO) ; " +
        "ouvrir_porte (x,y voisine) ; briser (contenant voisin) ; manette (manette voisine) ; activer (alcôve ou autel voisin) ; coffre (ouvrir ou fermer un coffre voisin) ; " +
        "spacio_magus (téléportation en x,y) ; spacio_creatura (échange des créatures en x,y et x2,y2). " +
        "Le coup est refusé (tour_perime) si la partie a avancé depuis votre dernier état lu, par exemple si l'humain a joué.")]
    public async Task<CallToolResult> Play(
        [Description("attendre, deplacer, ouvrir_porte, briser, manette, activer, coffre, spacio_magus ou spacio_creatura.")] string action,
        [Description("Colonne de la case visée.")] int? x = null,
        [Description("Ligne de la case visée.")] int? y = null,
        [Description("Colonne de la seconde créature (spacio_creatura).")] int? x2 = null,
        [Description("Ligne de la seconde créature (spacio_creatura).")] int? y2 = null,
        [Description("Pour deplacer, à la place de x,y : N, NE, E, SE, S, SO, O ou NO.")] string? direction = null,
        CancellationToken cancellationToken = default)
    {
        var known = _connection.LastState;
        if (known is null)
        {
            var current = await _connection.SendAsync(id => new RemoteRequest(id, RemoteCommands.State), cancellationToken);
            known = current.State ?? throw new McpException("Aucune partie en cours : lancez un niveau depuis le menu (outil interface) ou avec lancer_niveau.");
        }

        var gameAction = ToAction(action, x, y, x2, y2, direction, known.Magus.Position);
        var turn = known.Turn;
        return Result(await _connection.SendAsync(id => new RemoteRequest(id, RemoteCommands.Play, turn, gameAction), cancellationToken));
    }

    [McpServerTool(Name = "interface"), Description(
        "Utilise un contrôle de l'écran hors partie ou de la pause (bouton, liste, curseur, porte de niveau, fermeture d'info-bulle) " +
        "par son id tel que listé dans « Contrôles à l'écran ». Les id changent quand l'écran est reconstruit : relisez-les dans la dernière réponse.")]
    public async Task<CallToolResult> Interface(
        [Description("Id du contrôle, par exemple 44258297500 ou 49761224476:0.")] string controle,
        [Description("Pour une liste : index de l'option. Pour un curseur : valeur dans ses bornes. Ignoré pour un bouton.")] int? valeur = null,
        CancellationToken cancellationToken = default) =>
        Result(await _connection.SendAsync(id => new RemoteRequest(id, RemoteCommands.Ui, Control: controle, Value: valeur), cancellationToken));

    [McpServerTool(Name = "niveaux", ReadOnly = true), Description("Liste des niveaux avec nom, verrouillage, étoiles et record.")]
    public async Task<CallToolResult> Levels(CancellationToken cancellationToken = default) =>
        Result(await _connection.SendAsync(id => new RemoteRequest(id, RemoteCommands.Levels), cancellationToken));

    [McpServerTool(Name = "lancer_niveau"), Description(
        "Raccourci : démarre directement un niveau déverrouillé (fichier, ex. niveau_01.txt), sans passer par les menus ni la confirmation. " +
        "La partie en cours est remplacée.")]
    public async Task<CallToolResult> StartLevel(
        [Description("Fichier du niveau, tel que listé par l'outil niveaux.")] string fichier,
        CancellationToken cancellationToken = default) =>
        Result(await _connection.SendAsync(id => new RemoteRequest(id, RemoteCommands.Start, Level: fichier), cancellationToken));

    [McpServerTool(Name = "reprendre"), Description("Raccourci : quitte la pause, ou reprend la partie sauvegardée depuis le menu principal.")]
    public async Task<CallToolResult> Resume(CancellationToken cancellationToken = default) =>
        Result(await _connection.SendAsync(id => new RemoteRequest(id, RemoteCommands.Resume), cancellationToken));

    [McpServerTool(Name = "regles", ReadOnly = true), Description(
        "Règles du jeu telles que le joueur peut les lire (sorts, créatures, éléments), valeurs de base et légende de la carte.")]
    public string Rules()
    {
        if (string.IsNullOrWhiteSpace(_settings.ProjectPath))
        {
            throw new McpException("Dossier du projet non configuré (argument --projet=).");
        }

        return RulesText.Build(_settings.ProjectPath);
    }

    private static GameAction ToAction(string action, int? x, int? y, int? x2, int? y2, string? direction, Position magus)
    {
        Position Target()
        {
            if (x is { } column && y is { } row)
            {
                return new Position(column, row);
            }

            throw new McpException($"L'action « {action} » exige x et y.");
        }

        switch (action.Trim().ToLowerInvariant())
        {
            case "attendre":
                return new WaitAction();
            case "deplacer":
                if (direction is not null)
                {
                    if (!Directions.TryGetValue(direction.Trim(), out var step))
                    {
                        throw new McpException($"Direction « {direction} » inconnue : N, NE, E, SE, S, SO, O ou NO.");
                    }

                    return new MoveAction(magus + step);
                }

                return new MoveAction(Target());
            case "ouvrir_porte":
                return new OpenGrilleAction(Target());
            case "briser":
                return new BreakAction(Target());
            case "manette":
                return new LeverAction(Target());
            case "activer":
                return new ActivateAction(Target());
            case "coffre":
                return new ChestAction(Target());
            case "spacio_magus":
                return new SpacioMagusAction(Target());
            case "spacio_creatura":
                if (x2 is not { } secondColumn || y2 is not { } secondRow)
                {
                    throw new McpException("spacio_creatura exige x,y (première créature) et x2,y2 (seconde).");
                }

                return new SpacioCreaturaAction(Target(), new Position(secondColumn, secondRow));
            default:
                throw new McpException(
                    $"Action « {action} » inconnue : attendre, deplacer, ouvrir_porte, briser, manette, activer, coffre, spacio_magus ou spacio_creatura.");
        }
    }

    private static CallToolResult Result(RemoteResponse response, string? preamble = null)
    {
        var text = ResponseText.Format(response);
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = preamble is null ? text : preamble + "\n\n" + text }],
            IsError = !response.Ok,
        };
    }
}
