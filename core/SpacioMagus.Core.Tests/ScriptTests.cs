using SpacioMagus.Core.Loading;
using SpacioMagus.Core.Saving;

namespace SpacioMagus.Core.Tests;

public class ScriptTests
{
    private static readonly Position Alcove = new(1, 19);

    private static readonly Lazy<LoadedLevel> Level2 = new(() =>
    {
        var game = FindGameDirectory();
        var legend = Legend.Parse("legende.txt", File.ReadAllText(Path.Combine(game, "data", "legende.txt")));
        return LevelFile.Parse("niveau_02.txt", File.ReadAllText(Path.Combine(game, "niveaux", "niveau_02.txt")), legend, RuleSet.Default);
    });

    private static Game NewGame(ulong seed, Position? start = null)
    {
        var level = Level2.Value.Definition;
        if (start is { } magus)
        {
            level = new LevelDefinition(
                level.Name, level.Width, level.Height, level.Cells, magus, Array.Empty<MonsterSpawn>(),
                level.Potions, level.Torches, level.Doors, level.Alcoves, level.Levers, level.Script);
        }

        return new Game(level, RuleSet.Default, new SeededRandom(seed));
    }

    private static int[] PotionsPerZone(Game game)
    {
        var withPotion = game.Containers.Where(game.ContainerHasPotion).ToList();
        return new[]
        {
            withPotion.Count(cell => cell.Y < 5),
            withPotion.Count(cell => cell.Y is > 6 and < 10),
            withPotion.Count(cell => cell.Y is > 10 and < 14),
            withPotion.Count(cell => cell.Y is > 15 and < 17),
            withPotion.Count(cell => cell.Y > 17),
        };
    }

    [Fact]
    public void Level2HidesOneTwoAndTwoPotionsFromTopToStart()
    {
        for (var seed = 1UL; seed <= 30; seed++)
        {
            var game = NewGame(seed);
            Assert.Equal(16, game.Containers.Count);
            Assert.Equal(new[] { 1, 1, 1, 1, 1 }, PotionsPerZone(game));
        }
    }

    [Fact]
    public void SameSeedGivesTheSameDraw()
    {
        var draws = new HashSet<string>();
        for (var seed = 1UL; seed <= 30; seed++)
        {
            var first = string.Join(" ", NewGame(seed).Containers.Where(NewGame(seed).ContainerHasPotion).OrderBy(c => c.Y).ThenBy(c => c.X));
            var again = NewGame(seed);
            var second = string.Join(" ", again.Containers.Where(again.ContainerHasPotion).OrderBy(c => c.Y).ThenBy(c => c.X));
            Assert.Equal(first, second);
            draws.Add(first);
        }

        Assert.True(draws.Count > 1);
    }

    [Fact]
    public void AlcoveMustBeActivatedFromANeighbourCell()
    {
        var game = NewGame(1);

        Assert.Equal(ActionError.NotAdjacent, game.Activate(Alcove).Error);
        Assert.Equal(ActionError.NotActivable, game.Activate(new Position(4, 4)).Error);
        Assert.False(game.PotionsAreRevealed);
    }

    [Fact]
    public void LitAlcoveRevealsPotionsOnce()
    {
        var game = NewGame(1, new Position(1, 20));
        Assert.Contains(Alcove, game.Activables());

        var result = game.Activate(Alcove);

        Assert.True(result.Success);
        Assert.Equal(1, game.Turn);
        Assert.Contains(result.Events, e => e is AlcoveLit { Position: var lit } && lit == Alcove);
        Assert.Contains(result.Events, e => e is PotionsRevealed);
        Assert.True(game.PotionsAreRevealed);
        Assert.True(game.IsActivated(Alcove));
        Assert.Empty(game.Activables());
        Assert.Equal(ActionError.AlreadyActive, game.Activate(Alcove).Error);
    }

    [Fact]
    public void LastLeverOpensEveryPreviousGrille()
    {
        var game = NewGame(1, new Position(6, 4));

        var result = game.PullLever(new Position(7, 4));

        Assert.True(result.Success);
        var opened = result.Events.OfType<GrilleOpened>().Select(e => e.Position).ToHashSet();
        Assert.Equal(new HashSet<Position> { new(1, 3), new(4, 6), new(4, 15) }, opened);
        Assert.Single(result.Events.OfType<LeverPulled>());
        Assert.All(opened, grille => Assert.Equal(RuleSet.Default.GrilleOpenDuration, game.GrilleAt(grille)!.TurnsRemaining));
    }

    [Fact]
    public void FirstLeverOpensOnlyItsGrille()
    {
        var game = NewGame(1, new Position(6, 18));

        var result = game.PullLever(new Position(7, 19));

        Assert.True(result.Success);
        Assert.Equal(new Position(4, 15), Assert.Single(result.Events.OfType<GrilleOpened>()).Position);
    }

    [Fact]
    public void LeverWithItsOwnGrilleOpenIsRefused()
    {
        var game = NewGame(1, new Position(6, 4));
        game.PullLever(new Position(7, 4));

        Assert.Equal(ActionError.GrilleAlreadyOpen, game.PullLever(new Position(7, 4)).Error);
    }

    [Fact]
    public void ActivationSurvivesASave()
    {
        var save = new SaveGame(Game.RulesVersion, "niveau_02.txt", null, 7, new GameAction[] { new ActivateAction(Alcove) }, null);

        var parsed = SaveCodec.Parse("sauvegarde.json", SaveCodec.Serialize(save));

        Assert.Equal(new ActivateAction(Alcove), Assert.Single(parsed.Actions));
    }

    [Fact]
    public void UnknownScriptIsRejected()
    {
        var legend = Legend.Parse("legende.txt", """
            [legende]
            # = mur
            . = sol
            > = escalier
            @ = depart
            """);

        var error = Assert.Throws<LevelLoadException>(() => LevelFile.Parse("niveau_test.txt", """
            [niveau]
            script = inconnu

            [carte]
            #####
            #@.>#
            #####
            """, legend, RuleSet.Default));

        Assert.Contains(error.Issues, issue => issue.Message.Contains("Script de niveau inconnu"));
    }

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
