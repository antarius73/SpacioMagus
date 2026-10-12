using SpacioMagus.Core.Loading;
using SpacioMagus.Core.Saving;

namespace SpacioMagus.Core.Tests;

public class SaveTests
{
    private const string LevelFile = "niveau_test.txt";
    private const string Fingerprint = "empreinte";

    private static readonly string[] Gate =
    {
        "#######",
        "#>....#",
        "###G###",
        "#..@..#",
        "#######",
        "#b#####",
        "#######",
    };

    private static readonly Position GrillePosition = new(3, 2);

    private static readonly GameAction[] Script =
    {
        new OpenGrilleAction(GrillePosition),
        new MoveAction(GrillePosition),
        new WaitAction(),
        new WaitAction(),
        new WaitAction(),
        new WaitAction(),
        new WaitAction(),
    };

    [Fact]
    public void SeededRandomProducesAFrozenSequence()
    {
        var random = new SeededRandom(42);

        var values = Enumerable.Range(0, 6).Select(_ => random.Next(1000)).ToArray();

        Assert.Equal(new[] { 413, 291, 858, 764, 250, 62 }, values);
    }

    [Fact]
    public void SeededRandomRejectsAnEmptyRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeededRandom(1).Next(0));
    }

    [Fact]
    public void JournalRecordsOnlySuccessfulActions()
    {
        var journal = new GameJournal(TestMaps.Parse(Gate), RuleSet.Default, 7);

        Assert.False(journal.Play(new MoveAction(new Position(1, 1))).Success);
        Assert.True(journal.Play(new WaitAction()).Success);

        Assert.Equal(new GameAction[] { new WaitAction() }, journal.Actions);
    }

    [Fact]
    public void ReplayReproducesRandomDoorPushesExactly()
    {
        var landings = new HashSet<Position>();
        for (ulong seed = 0; seed < 40; seed++)
        {
            var original = Play(seed);
            landings.Add(original.Game.Magus.Position);

            var restored = SaveRestorer.Restore(Save(original), TestMaps.Parse(Gate), RuleSet.Default, Fingerprint);

            Assert.False(restored.FellBack);
            AssertSameState(original.Game, restored.Journal.Game);
            Assert.Equal(original.Actions, restored.Journal.Actions);
        }

        Assert.Contains(new Position(3, 1), landings);
        Assert.Contains(new Position(3, 3), landings);
    }

    [Fact]
    public void RestoredGameKeepsTheSameFuture()
    {
        var original = Play(3);
        var restored = SaveRestorer.Restore(Save(original), TestMaps.Parse(Gate), RuleSet.Default, Fingerprint).Journal;

        foreach (var action in new GameAction[] { new OpenGrilleAction(GrillePosition), new MoveAction(GrillePosition) }
                     .Concat(Enumerable.Repeat<GameAction>(new WaitAction(), 5)))
        {
            Assert.Equal(original.Play(action).Success, restored.Play(action).Success);
        }

        AssertSameState(original.Game, restored.Game);
    }

    [Fact]
    public void LevelStartSaveStartsAFreshGameWithItsSeed()
    {
        var restored = SaveRestorer.Restore(SaveGame.LevelStart(LevelFile, 5), TestMaps.Parse(Gate), RuleSet.Default, Fingerprint);

        Assert.False(restored.FellBack);
        Assert.Equal(5UL, restored.Journal.Seed);
        Assert.Equal(0, restored.Journal.Game.Turn);
    }

    [Fact]
    public void ChangedLevelRestartsTheLevel()
    {
        var restored = SaveRestorer.Restore(Save(Play(1)), TestMaps.Parse(Gate), RuleSet.Default, "autre empreinte");

        AssertRestarted(restored);
    }

    [Fact]
    public void OtherRulesVersionRestartsTheLevel()
    {
        var save = Save(Play(1)) with { RulesVersion = Game.RulesVersion + 1 };

        AssertRestarted(SaveRestorer.Restore(save, TestMaps.Parse(Gate), RuleSet.Default, Fingerprint));
    }

    [Fact]
    public void RefusedActionRestartsTheLevel()
    {
        var save = Save(Play(1)) with { Actions = new GameAction[] { new MoveAction(new Position(1, 1)) } };

        AssertRestarted(SaveRestorer.Restore(save, TestMaps.Parse(Gate), RuleSet.Default, Fingerprint));
    }

    [Fact]
    public void DifferentReplayedStateRestartsTheLevel()
    {
        var save = Save(Play(1)) with { Checksum = "0000" };

        AssertRestarted(SaveRestorer.Restore(save, TestMaps.Parse(Gate), RuleSet.Default, Fingerprint));
    }

    [Fact]
    public void CodecRoundTripsEveryAction()
    {
        var save = new SaveGame(
            Game.RulesVersion,
            LevelFile,
            Fingerprint,
            ulong.MaxValue,
            new GameAction[]
            {
                new MoveAction(new Position(1, 2)),
                new WaitAction(),
                new OpenGrilleAction(new Position(3, 4)),
                new SpacioMagusAction(new Position(5, 6)),
                new SpacioCreaturaAction(new Position(7, 8), new Position(9, 10)),
            },
            "abc");

        var parsed = SaveCodec.Parse("sauvegarde.json", SaveCodec.Serialize(save));

        Assert.Equal(save with { Actions = Array.Empty<GameAction>() }, parsed with { Actions = Array.Empty<GameAction>() });
        Assert.Equal(save.Actions, parsed.Actions);
    }

    [Fact]
    public void CodecRoundTripsALevelStart()
    {
        var save = SaveGame.LevelStart(LevelFile, 12);

        var parsed = SaveCodec.Parse("sauvegarde.json", SaveCodec.Serialize(save));

        Assert.Null(parsed.Fingerprint);
        Assert.Null(parsed.Checksum);
        Assert.Empty(parsed.Actions);
        Assert.Equal(12UL, parsed.Seed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ pas du json")]
    [InlineData("[]")]
    [InlineData("{\"format\":99,\"regles\":1,\"niveau\":\"a.txt\",\"graine\":1,\"actions\":[]}")]
    [InlineData("{\"format\":1,\"regles\":1,\"niveau\":\"a.txt\",\"graine\":1,\"actions\":[{\"type\":\"voler\"}]}")]
    [InlineData("{\"format\":1,\"regles\":1,\"niveau\":\"a.txt\",\"graine\":-1,\"actions\":[]}")]
    [InlineData("{\"format\":1,\"regles\":1,\"niveau\":\"a.txt\",\"graine\":1,\"actions\":[{\"type\":\"marcher\",\"x\":1}]}")]
    [InlineData("{\"format\":1,\"regles\":1,\"graine\":1,\"actions\":[]}")]
    public void CodecRejectsInvalidContent(string content)
    {
        Assert.Throws<LevelLoadException>(() => SaveCodec.Parse("sauvegarde.json", content));
    }

    [Fact]
    public void FingerprintIgnoresLineEndingsButNotContent()
    {
        Assert.Equal(GameDigest.Fingerprint("a\nb", "c"), GameDigest.Fingerprint("a\r\nb", "c"));
        Assert.NotEqual(GameDigest.Fingerprint("a", "bc"), GameDigest.Fingerprint("ab", "c"));
        Assert.NotEqual(GameDigest.Fingerprint("a\nb"), GameDigest.Fingerprint("a\nc"));
    }

    [Fact]
    public void CoreRulesUseNoHiddenRandomness()
    {
        var forbidden = new[] { "new Random(", "Random.Shared", "DateTime.Now", "DateTime.UtcNow", "Guid.NewGuid", "Environment.TickCount", "Stopwatch" };
        var allowed = new[] { "IRandomSource.cs" };
        var problems = new List<string>();
        foreach (var path in Directory.EnumerateFiles(CoreSourceDirectory(), "*.cs", SearchOption.AllDirectories))
        {
            var separator = Path.DirectorySeparatorChar;
            if (path.Contains($"{separator}bin{separator}") || path.Contains($"{separator}obj{separator}") || allowed.Contains(Path.GetFileName(path)))
            {
                continue;
            }

            var lines = File.ReadAllLines(path);
            for (var index = 0; index < lines.Length; index++)
            {
                foreach (var pattern in forbidden.Where(pattern => lines[index].Contains(pattern, StringComparison.Ordinal)))
                {
                    problems.Add($"{Path.GetFileName(path)} ligne {index + 1} : « {pattern} ».");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    private static GameJournal Play(ulong seed)
    {
        var journal = new GameJournal(TestMaps.Parse(Gate), RuleSet.Default, seed);
        foreach (var action in Script)
        {
            journal.Play(action);
        }

        return journal;
    }

    private static SaveGame Save(GameJournal journal) => SaveGame.InProgress(LevelFile, Fingerprint, journal);

    private static void AssertRestarted(RestoredGame restored)
    {
        Assert.True(restored.FellBack);
        Assert.Empty(restored.Journal.Actions);
        Assert.Equal(0, restored.Journal.Game.Turn);
    }

    private static void AssertSameState(Game expected, Game actual)
    {
        Assert.Equal(GameDigest.Compute(expected), GameDigest.Compute(actual));
        Assert.Equal(expected.Turn, actual.Turn);
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.Magus.Position, actual.Magus.Position);
        Assert.Equal(expected.Magus.Magic, actual.Magus.Magic);
        Assert.Equal(
            expected.Monsters.Select(monster => (monster.Id, monster.Position, monster.StunTurns, monster.IsTracking)),
            actual.Monsters.Select(monster => (monster.Id, monster.Position, monster.StunTurns, monster.IsTracking)));
        Assert.Equal(
            expected.Grilles.OrderBy(grille => grille.Position.Y).Select(grille => (grille.Position, grille.TurnsRemaining)),
            actual.Grilles.OrderBy(grille => grille.Position.Y).Select(grille => (grille.Position, grille.TurnsRemaining)));
    }

    private static string CoreSourceDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "SpacioMagus.Core");
            if (File.Exists(Path.Combine(candidate, "SpacioMagus.Core.csproj")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"Sources de SpacioMagus.Core introuvables depuis {AppContext.BaseDirectory}.");
    }
}
