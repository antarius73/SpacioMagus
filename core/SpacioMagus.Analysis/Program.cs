using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using SpacioMagus.Analysis;
using SpacioMagus.Core;
using SpacioMagus.Core.Loading;
using SpacioMagus.Core.Saving;

var culture = CultureInfo.GetCultureInfo("fr-FR");
CultureInfo.CurrentCulture = culture;

Options options;
try
{
    options = Options.Parse(args);
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    Console.Error.WriteLine(Options.Usage);
    return 2;
}

var levels = new List<LoadedAnalysisLevel>();
foreach (var path in options.Levels)
{
    try
    {
        levels.Add(LevelLoader.Load(path, options.DataDirectory));
    }
    catch (Exception exception) when (exception is FileNotFoundException or LevelLoadException or IOException)
    {
        Console.Error.WriteLine($"{path} : {exception.Message}");
        return 1;
    }
}

if (options.ReplayPath is { } replayPath)
{
    return Replay(replayPath, levels);
}

var reports = levels.Select(level => Analyse(level, options)).ToList();

if (reports.Count > 1)
{
    PrintComparison(reports);
}

if (options.JsonPath is { } jsonPath)
{
    try
    {
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(reports, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Rapport JSON : {Path.GetFullPath(jsonPath)}");
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"Écriture du rapport impossible : {exception.Message}");
        return 1;
    }
}

return 0;

static int Replay(string path, IReadOnlyList<LoadedAnalysisLevel> levels)
{
    SaveGame save;
    try
    {
        save = SaveCodec.Parse(Path.GetFileName(path), File.ReadAllText(path));
    }
    catch (Exception exception) when (exception is IOException or LevelLoadException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"Partie illisible : {exception.Message}");
        return 1;
    }

    var level = levels.FirstOrDefault(candidate => candidate.FileName.Equals(save.LevelFile, StringComparison.OrdinalIgnoreCase));
    if (level is null)
    {
        Console.Error.WriteLine($"La partie concerne « {save.LevelFile} », absent des niveaux indiqués.");
        return 1;
    }

    Console.WriteLine($"=== Rejeu de {Path.GetFileName(path)} sur {level.FileName} (graine {save.Seed}, règles v{save.RulesVersion}, actuelles v{Game.RulesVersion}) ===");
    var journal = new GameJournal(level.Definition, level.Rules, save.Seed);
    for (var index = 0; index < save.Actions.Count; index++)
    {
        var action = save.Actions[index];
        var result = journal.Play(action);
        Console.WriteLine($"{index + 1,3}. {ActionGenerator.Describe(action)}{(result.Success ? string.Empty : $"  ← REFUSÉ : {result.Error}")}");
        if (!result.Success || journal.Game.Status != GameStatus.Playing)
        {
            break;
        }
    }

    Console.WriteLine($"Issue : {journal.Game.Status} au tour {journal.Game.Turn}.");
    Console.WriteLine("Séquence C# :");
    foreach (var action in journal.Actions)
    {
        Console.WriteLine($"            {CSharp(action)},");
    }

    return journal.Game.Status == GameStatus.Won ? 0 : 3;
}

static string CSharp(GameAction action) => action switch
{
    MoveAction move => $"new MoveAction(new Position({move.Target.X}, {move.Target.Y}))",
    WaitAction => "new WaitAction()",
    OpenGrilleAction grille => $"new OpenGrilleAction(new Position({grille.Target.X}, {grille.Target.Y}))",
    BreakAction breaking => $"new BreakAction(new Position({breaking.Target.X}, {breaking.Target.Y}))",
    LeverAction lever => $"new LeverAction(new Position({lever.Target.X}, {lever.Target.Y}))",
    ActivateAction activate => $"new ActivateAction(new Position({activate.Target.X}, {activate.Target.Y}))",
    SpacioMagusAction teleport => $"new SpacioMagusAction(new Position({teleport.Target.X}, {teleport.Target.Y}))",
    SpacioCreaturaAction swap => $"new SpacioCreaturaAction(new Position({swap.First.X}, {swap.First.Y}), new Position({swap.Second.X}, {swap.Second.Y}))",
    _ => action.ToString() ?? "?",
};

static LevelReport Analyse(LoadedAnalysisLevel level, Options options)
{
    Console.WriteLine();
    Console.WriteLine($"=== {level.FileName} — {level.Definition.Name} ===");
    var start = new Game(level.Definition, level.Rules, new ForcedRandom(0));
    var first = new ForcedRandom(0);
    var second = new ForcedRandom(1);

    var clock = Stopwatch.StartNew();
    var search = Searcher.FindSolution(start, first, ActionFilter.All, options.MaxStates);
    clock.Stop();
    if (search.Solution is { } found)
    {
        Console.WriteLine($"Recherche dirigée : solution en {found.Count} tours ({search.States:N0} états, {clock.Elapsed.TotalSeconds:N1} s) — Spacio Magus ×{found.Count(a => a is SpacioMagusAction)}, Spacio Creatura ×{found.Count(a => a is SpacioCreaturaAction)}, bris ×{found.Count(a => a is BreakAction)}, attentes ×{found.Count(a => a is WaitAction)}.");
        Console.WriteLine($"  {string.Join(" ; ", found.Select(ActionGenerator.Describe))}");
    }
    else
    {
        Console.WriteLine(search.Truncated
            ? $"Recherche dirigée : aucune solution dans la limite de {search.States:N0} états (augmenter --max-etats)."
            : $"Recherche dirigée : AUCUNE solution, tous les états accessibles ont été examinés ({search.States:N0}).");
    }

    var other = Searcher.FindSolution(start, second, ActionFilter.All, options.MaxStates);
    Console.WriteLine($"Portes qui repoussent de l'autre côté : {(other.Solution is { } alternative ? $"solution en {alternative.Count} tours" : "aucune solution trouvée")}");

    var players = new Players(level.Definition, first);
    var hurried = players.Greedy(start, false, false);
    var cautious = players.Greedy(start, true, false);
    Console.WriteLine($"Joueur pressé (téléportation vers l'escalier, sans regarder les monstres) : {Describe(hurried)}");
    Console.WriteLine($"Joueur prudent (pareil, mais évite la défaite immédiate) : {Describe(cautious)}");
    var playouts = players.Playouts(start, options.Playouts, options.Exploration, options.Seed);
    Console.WriteLine($"Joueurs semi-aléatoires ({playouts.Games:N0} parties, {options.Exploration:P0} de coups au hasard) : {playouts.WinRate:P0} de victoires, {playouts.Lost:N0} défaites (tour moyen {playouts.AverageLossTurn:N1}), {playouts.Stuck:N0} sans issue.");

    ExplorationResult? main = null;
    var critical = new List<PathStep>();
    if (!options.SearchOnly)
    {
        var depth = search.Solution?.Count ?? int.MaxValue;
        clock.Restart();
        main = Explorer.Explore(start, first, ActionFilter.All, options.MaxStates, false, depth);
        clock.Stop();
        var complete = !main.Truncated;
        Console.WriteLine($"Exploration complète jusqu'à {(depth == int.MaxValue ? "épuisement" : $"{depth} tours")} : {main.States:N0} états, {main.Transitions:N0} coups, {clock.Elapsed.TotalSeconds:N1} s{(complete ? string.Empty : " — TRONQUÉE, chiffres suivants partiels")}");
        if (main.Solvable)
        {
            Console.WriteLine($"Solution la plus courte (garantie) : {main.ShortestWin} tours ; nombre de solutions de cette longueur : {main.ShortestSolutions:N0}");
            critical = main.Steps.Where(step => step.LegalActions > 1 && step.WinningActions * 100 <= step.LegalActions * options.CriticalPercent).ToList();
            Console.WriteLine($"Moments critiques (au plus {options.CriticalPercent} % des coups gardent la victoire possible) : {critical.Count}");
            foreach (var step in critical)
            {
                Console.WriteLine($"  tour {step.Turn} : {step.Action} — {step.WinningActions} coup(s) gagnant(s) sur {step.LegalActions}, {step.LosingActions} perdant(s) immédiatement");
            }

            if (options.ShowSolution)
            {
                foreach (var step in main.Steps)
                {
                    Console.WriteLine($"  {step.Turn,2}. {step.Action}  ({step.WinningActions}/{step.LegalActions})");
                }
            }
        }
        else
        {
            Console.WriteLine(complete
                ? "Exploration complète : aucune solution plus courte que celle de la recherche dirigée."
                : "Exploration complète : n'a pas atteint de victoire dans la limite ; la longueur minimale n'est pas garantie.");
        }

        Console.WriteLine($"Coups qui font perdre immédiatement : {Percent(main.LosingTransitions, main.Transitions)}");
    }

    int? withoutSwap = null, withoutTeleport = null, lessMagic = null;
    if (options.Complete)
    {
        withoutSwap = Searcher.FindSolution(start, first, new ActionFilter(true, false), options.MaxStates).Solution?.Count;
        withoutTeleport = Searcher.FindSolution(start, first, new ActionFilter(false, true), options.MaxStates).Solution?.Count;
        if (level.Rules.MagicMax > 1)
        {
            var poorer = new Game(level.Definition, level.Rules with { MagicMax = level.Rules.MagicMax - 1 }, first);
            lessMagic = Searcher.FindSolution(poorer, first, ActionFilter.All, options.MaxStates).Solution?.Count;
        }

        Console.WriteLine($"Sans Spacio Creatura : {Turns(withoutSwap)}");
        Console.WriteLine($"Sans Spacio Magus : {Turns(withoutTeleport)}");
        Console.WriteLine($"Avec une gemme de moins : {Turns(lessMagic)}");
    }

    var solution = main is { Solvable: true } ? main.Solution : search.Solution ?? Array.Empty<GameAction>();
    ReportStars(level, main is { Solvable: true, Truncated: false } ? main.ShortestWin : null, solution.Count);
    var teleports = solution.Count(a => a is SpacioMagusAction);
    var swaps = solution.Count(a => a is SpacioCreaturaAction);
    var goals = new[]
    {
        ("le joueur pressé perd", hurried.Outcome != PlayOutcome.Won),
        ("la solution utilise les deux sorts", teleports > 0 && swaps > 0),
        ("au moins 2 solutions les plus courtes", (main?.ShortestSolutions ?? 0) >= 2),
    };
    Console.WriteLine("Objectifs de difficulté : " + string.Join(" ; ", goals.Select(goal => $"{goal.Item1} {(goal.Item2 ? "OUI" : "non")}")));

    return new LevelReport(
        level.FileName,
        level.Definition.Name,
        main?.States ?? search.States,
        main?.Truncated ?? search.Truncated,
        main?.ShortestWin ?? search.Solution?.Count,
        main?.ShortestSolutions ?? 0,
        (teleports + swaps) * level.Rules.SpellCost,
        teleports,
        swaps,
        main is null || main.States == 0 ? 0 : (double)main.WinnableStates / main.States,
        main is null || main.Transitions == 0 ? 0 : (double)main.LosingTransitions / main.Transitions,
        other.Solution?.Count,
        hurried.Outcome.ToString(),
        cautious.Outcome.ToString(),
        playouts.WinRate,
        critical.Count,
        withoutSwap,
        withoutTeleport,
        lessMagic,
        solution.Select(ActionGenerator.Describe).ToList());
}

static void ReportStars(LoadedAnalysisLevel level, int? guaranteed, int best)
{
    if (best == 0)
    {
        Console.WriteLine("Étoiles : pas de proposition sans solution connue.");
        return;
    }

    var twoStars = (int)Math.Ceiling(best * 1.3);
    Console.WriteLine($"Étoiles proposées : etoiles = {best}, {twoStars}{(guaranteed is null ? " (meilleure solution trouvée, la plus courte n'est pas garantie)" : " (solution la plus courte garantie)")}");
    if (level.Stars is not { } stars)
    {
        Console.WriteLine("Étoiles du niveau : ABSENTES de [niveau].");
        return;
    }

    Console.WriteLine($"Étoiles du niveau : 3 étoiles en {stars.ThreeStars} tours au plus, 2 étoiles en {stars.TwoStars} tours au plus.");
    if (guaranteed is { } shortest && stars.ThreeStars < shortest)
    {
        Console.WriteLine($"  ATTENTION : 3 étoiles impossibles, la solution la plus courte fait {shortest} tours.");
    }
    else if (guaranteed is null && stars.ThreeStars < best)
    {
        Console.WriteLine($"  À vérifier : seuil des 3 étoiles sous la meilleure solution trouvée ({best} tours) ; possible seulement si une solution plus courte existe.");
    }
    else if (stars.ThreeStars > best + Math.Max(2, best / 5))
    {
        Console.WriteLine($"  Remarque : 3 étoiles faciles, la meilleure solution connue fait {best} tours.");
    }
}

static string Percent(double part, double total) => total == 0 ? "—" : (part / total).ToString("P1");

static string Turns(int? turns) => turns is { } value ? $"faisable en {value} tours" : "aucune solution trouvée";

static string Describe(PlayResult result) => result.Outcome switch
{
    PlayOutcome.Won => $"GAGNE en {result.Turns} tours",
    PlayOutcome.Lost => $"perd au tour {result.Turns}",
    _ => $"bloqué au tour {result.Turns}",
};

static void PrintComparison(IReadOnlyList<LevelReport> reports)
{
    Console.WriteLine();
    Console.WriteLine("=== Comparaison ===");
    Console.WriteLine($"{"Niveau",-28} {"Tours",6} {"Sol.",8} {"Gemmes",7} {"Pressé",8} {"Aléatoire",10} {"Critiques",10}");
    foreach (var report in reports)
    {
        Console.WriteLine($"{report.File,-28} {(report.ShortestWin?.ToString() ?? "—"),6} {report.ShortestSolutions,8:N0} {report.MagicSpent,7} {report.HurriedPlayer,8} {report.PlayoutWinRate,10:P0} {report.CriticalMoments,10}");
    }
}

public sealed record LevelReport(
    string File,
    string Name,
    int States,
    bool Truncated,
    int? ShortestWin,
    double ShortestSolutions,
    int MagicSpent,
    int SpacioMagusCasts,
    int SpacioCreaturaCasts,
    double WinnableShare,
    double ImmediateLossShare,
    int? ShortestWinOtherPush,
    string HurriedPlayer,
    string CautiousPlayer,
    double PlayoutWinRate,
    int CriticalMoments,
    int? WithoutSwap,
    int? WithoutTeleport,
    int? WithLessMagic,
    IReadOnlyList<string> Solution);

public sealed class Options
{
    public const string Usage = """
        Usage : dotnet run --project core/SpacioMagus.Analysis -- <niveau.txt> [autres niveaux...] [options]
          --donnees <dossier>   dossier contenant regles.txt et legende.txt (par défaut : ../data à côté du niveau)
          --max-etats <n>       limite d'états explorés (défaut 500 000)
          --parties <n>         nombre de parties semi-aléatoires (défaut 1 000)
          --hasard <0..1>       part de coups au hasard des parties semi-aléatoires (défaut 0,4)
          --graine <n>          graine des parties semi-aléatoires (défaut 1)
          --critique <%>        seuil des moments critiques (défaut 30)
          --complet             vérifie aussi le niveau sans chaque sort et avec une gemme de moins
          --solution            affiche la solution la plus courte complète
          --recherche-seule     uniquement la recherche dirigée (rapide)
          --rejouer <partie>    rejoue une partie enregistrée par le jeu (user://parties) sur le niveau indiqué
          --json <fichier>      écrit le rapport en JSON
        """;

    public List<string> Levels { get; } = new();

    public string? DataDirectory { get; private set; }

    public int MaxStates { get; private set; } = 500_000;

    public int Playouts { get; private set; } = 1_000;

    public double Exploration { get; private set; } = 0.4;

    public int Seed { get; private set; } = 1;

    public int CriticalPercent { get; private set; } = 30;

    public bool Complete { get; private set; }

    public bool ShowSolution { get; private set; }

    public bool SearchOnly { get; private set; }

    public string? ReplayPath { get; private set; }

    public string? JsonPath { get; private set; }

    public static Options Parse(string[] args)
    {
        var options = new Options();
        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            string Value()
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException($"Valeur manquante après {arg}.");
                }

                return args[++index];
            }

            int Integer(int minimum)
            {
                var text = Value();
                if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) || number < minimum)
                {
                    throw new ArgumentException($"{arg} attend un entier ≥ {minimum} : « {text} ».");
                }

                return number;
            }

            switch (arg)
            {
                case "--donnees":
                    options.DataDirectory = Value();
                    break;
                case "--max-etats":
                    options.MaxStates = Integer(1);
                    break;
                case "--parties":
                    options.Playouts = Integer(0);
                    break;
                case "--graine":
                    options.Seed = Integer(0);
                    break;
                case "--critique":
                    options.CriticalPercent = Integer(0);
                    break;
                case "--hasard":
                    var text = Value().Replace(',', '.');
                    if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var share) || share is < 0 or > 1)
                    {
                        throw new ArgumentException($"--hasard attend un nombre entre 0 et 1 : « {text} ».");
                    }

                    options.Exploration = share;
                    break;
                case "--complet":
                    options.Complete = true;
                    break;
                case "--solution":
                    options.ShowSolution = true;
                    break;
                case "--recherche-seule":
                    options.SearchOnly = true;
                    break;
                case "--rejouer":
                    options.ReplayPath = Value();
                    break;
                case "--json":
                    options.JsonPath = Value();
                    break;
                default:
                    if (arg.StartsWith("--", StringComparison.Ordinal))
                    {
                        throw new ArgumentException($"Option inconnue : {arg}.");
                    }

                    options.Levels.AddRange(Expand(arg));
                    break;
            }
        }

        if (options.Levels.Count == 0)
        {
            throw new ArgumentException("Aucun niveau indiqué.");
        }

        return options;
    }

    private static IEnumerable<string> Expand(string pattern)
    {
        if (pattern.IndexOfAny(new[] { '*', '?' }) < 0)
        {
            return new[] { pattern };
        }

        var directory = Path.GetDirectoryName(pattern);
        var folder = string.IsNullOrEmpty(directory) ? "." : directory;
        if (!Directory.Exists(folder))
        {
            throw new ArgumentException($"Dossier introuvable : {folder}.");
        }

        var files = Directory.GetFiles(folder, Path.GetFileName(pattern)).OrderBy(file => file, StringComparer.OrdinalIgnoreCase).ToList();
        if (files.Count == 0)
        {
            throw new ArgumentException($"Aucun fichier ne correspond à {pattern}.");
        }

        return files;
    }
}
