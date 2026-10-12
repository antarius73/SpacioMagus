using SpacioMagus.Core.Loading;

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
        }
    }

    [Fact]
    public void Level01CanBeWonWithSpells()
    {
        var game = NewLevel01();

        Play(game,
            g => g.Move(new Position(3, 20)),
            g => g.Move(new Position(4, 19)),
            g => g.OpenGrille(new Position(4, 18)),
            g => g.Move(new Position(4, 18)),
            g => g.Move(new Position(4, 17)),
            g => g.Move(new Position(4, 16)),
            g => g.CastSpacioMagus(new Position(4, 12)),
            g => g.OpenGrille(new Position(4, 11)),
            g => g.Move(new Position(4, 11)),
            g => g.Move(new Position(4, 10)),
            g => g.Move(new Position(3, 9)),
            g => g.Move(new Position(2, 9)),
            g => g.Move(new Position(1, 9)),
            g => g.Move(new Position(2, 9)),
            g => g.Move(new Position(3, 9)),
            g => g.Move(new Position(4, 9)),
            g => g.Move(new Position(4, 8)),
            g => g.Move(new Position(4, 7)),
            g => g.Move(new Position(4, 6)),
            g => g.CastSpacioCreatura(new Position(2, 5), new Position(6, 5)),
            g => g.Move(new Position(4, 5)),
            g => g.Move(new Position(4, 4)),
            g => g.OpenGrille(new Position(4, 3)),
            g => g.CastSpacioMagus(new Position(4, 1)));

        Assert.Equal(GameStatus.Won, game.Status);
    }

    [Fact]
    public void Level01CorridorCannotBeCrossedOnFoot()
    {
        var game = NewLevel01();

        Play(game,
            g => g.Move(new Position(3, 20)),
            g => g.Move(new Position(4, 19)),
            g => g.OpenGrille(new Position(4, 18)),
            g => g.Move(new Position(4, 18)),
            g => g.Move(new Position(4, 17)),
            g => g.Move(new Position(4, 16)),
            g => g.Move(new Position(4, 15)),
            g => g.Move(new Position(4, 14)),
            g => g.Move(new Position(4, 13)),
            g => g.Move(new Position(4, 12)),
            g => g.OpenGrille(new Position(4, 11)));

        Assert.Equal(GameStatus.Lost, game.Status);
    }

    private static Game NewLevel01()
    {
        var settings = LoadSettings();
        var level = LevelFile.Parse(
            "niveau_01.txt",
            File.ReadAllText(Path.Combine(GameDirectory.Value, "niveaux", "niveau_01.txt")),
            LoadLegend(),
            settings.Rules);
        return new Game(level.Definition, level.Rules, new FixedRandom());
    }

    private static void Play(Game game, params Func<Game, ActionResult>[] actions)
    {
        for (var index = 0; index < actions.Length && game.Status == GameStatus.Playing; index++)
        {
            var result = actions[index](game);
            Assert.True(result.Success, $"Action {index + 1} refusée : {result.Error}.");
        }
    }

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
