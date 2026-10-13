using SpacioMagus.Core.Loading;
using SpacioMagus.Core.Saving;

namespace SpacioMagus.Core.Tests;

public class GameDataTests
{
    private static readonly Lazy<string> GameDirectory = new(FindGameDirectory);

    [Fact]
    public void AllListedLevelsLoadWithTheRealDataFiles()
    {
        var settings = LoadSettings();
        var legend = LoadLegend();
        var levels = LevelList.Parse("niveaux.txt", File.ReadAllText(DataPath("niveaux.txt")));

        foreach (var file in levels)
        {
            var path = Path.Combine(GameDirectory.Value, "niveaux", file);
            Assert.True(File.Exists(path), $"Fichier de niveau introuvable : {path}");
            var level = LevelFile.Parse(file, File.ReadAllText(path), legend, settings.Rules);
            Assert.NotNull(level.Definition);
            Assert.True(level.Stars is not null, $"{file} : seuils « etoiles » absents de [niveau].");
        }
    }

    [Fact]
    public void EverySoundFileExists()
    {
        var sounds = SoundTable.Parse("sons.txt", File.ReadAllText(DataPath("sons.txt")));

        Assert.NotEmpty(sounds);
        foreach (var (key, entry) in sounds)
        {
            foreach (var file in entry.Files)
            {
                var path = Path.Combine(GameDirectory.Value, "assets", "sons", file);
                Assert.True(File.Exists(path), $"{key} : fichier son introuvable {path}");
            }
        }
    }

    [Fact]
    public void Level01CanBeWonWithSpells()
    {
        var level = LoadLevel01();
        var journal = new GameJournal(level.Definition, level.Rules, 5233681745578830002UL);
        var actions = new GameAction[]
        {
            new MoveAction(new Position(5, 21)),
            new MoveAction(new Position(5, 20)),
            new MoveAction(new Position(4, 19)),
            new OpenGrilleAction(new Position(4, 18)),
            new SpacioCreaturaAction(new Position(4, 17), new Position(4, 15)),
            new SpacioMagusAction(new Position(4, 14)),
            new MoveAction(new Position(4, 13)),
            new MoveAction(new Position(4, 12)),
            new OpenGrilleAction(new Position(4, 11)),
            new MoveAction(new Position(4, 11)),
            new MoveAction(new Position(4, 10)),
            new SpacioCreaturaAction(new Position(4, 8), new Position(4, 6)),
            new SpacioMagusAction(new Position(4, 5)),
            new MoveAction(new Position(4, 4)),
            new OpenGrilleAction(new Position(4, 3)),
            new MoveAction(new Position(4, 3)),
            new MoveAction(new Position(4, 2)),
            new MoveAction(new Position(4, 1)),
        };

        for (var index = 0; index < actions.Length && journal.Game.Status == GameStatus.Playing; index++)
        {
            var result = journal.Play(actions[index]);
            Assert.True(result.Success, $"Action {index + 1} refusée : {result.Error}.");
        }

        Assert.Equal(GameStatus.Won, journal.Game.Status);
        Assert.Equal(actions.Length, journal.Game.Turn);
    }

    [Fact]
    public void Level01CorridorCannotBeCrossedOnFoot()
    {
        var game = NewLevel01();
        var path = new List<Position> { new(3, 20), new(4, 19) };
        for (var y = 18; y >= 1; y--)
        {
            path.Add(new Position(4, y));
        }

        var step = 0;
        while (game.Status == GameStatus.Playing && game.Turn < 40 && step < path.Count)
        {
            var target = path[step];
            if (game.GrilleAt(target) is { IsOpen: false })
            {
                Assert.True(game.OpenGrille(target).Success);
                continue;
            }

            if (game.Move(target).Success)
            {
                step++;
                continue;
            }

            Assert.True(game.Wait().Success);
        }

        Assert.Equal(GameStatus.Lost, game.Status);
    }

    private static Game NewLevel01()
    {
        var level = LoadLevel01();
        return new Game(level.Definition, level.Rules, new FixedRandom());
    }

    private static LoadedLevel LoadLevel01() =>
        LevelFile.Parse(
            "niveau_01.txt",
            File.ReadAllText(Path.Combine(GameDirectory.Value, "niveaux", "niveau_01.txt")),
            LoadLegend(),
            LoadSettings().Rules);

    private static GameSettings LoadSettings() =>
        GameSettings.Parse("regles.txt", File.ReadAllText(DataPath("regles.txt")));

    private static Legend LoadLegend() =>
        Legend.Parse("legende.txt", File.ReadAllText(DataPath("legende.txt")));

    private static string DataPath(string file) => Path.Combine(GameDirectory.Value, "data", file);

    private static string FindGameDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "game");
            if (File.Exists(Path.Combine(candidate, "data", "legende.txt")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"Dossier « game » introuvable en remontant depuis {AppContext.BaseDirectory}.");
    }
}
