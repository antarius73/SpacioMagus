using SpacioMagus.Core.Loading;
using SpacioMagus.Core.Progress;

namespace SpacioMagus.Core.Tests;

public class ProgressTests
{
    private const string Level = "niveau_01.txt";

    [Theory]
    [InlineData(10, 3)]
    [InlineData(14, 3)]
    [InlineData(15, 2)]
    [InlineData(18, 2)]
    [InlineData(19, 1)]
    [InlineData(200, 1)]
    public void StarsFollowTheTurnThresholds(int turns, int stars)
    {
        Assert.Equal(stars, new StarThresholds(14, 18).Rate(turns));
    }

    [Fact]
    public void EqualThresholdsSkipTheSecondStar()
    {
        var thresholds = new StarThresholds(10, 10);

        Assert.Equal(3, thresholds.Rate(10));
        Assert.Equal(1, thresholds.Rate(11));
    }

    [Fact]
    public void ThresholdsAreReadFromTheLevel()
    {
        var level = Load("""
            [niveau]
            etoiles = 14, 18

            [carte]
            #####
            #@.>#
            #####
            """);

        Assert.Equal(new StarThresholds(14, 18), level.Stars);
    }

    [Theory]
    [InlineData("14")]
    [InlineData("18, 14")]
    [InlineData("0, 5")]
    [InlineData("a, b")]
    [InlineData("1, 2, 3")]
    public void InvalidThresholdsAreRefused(string value)
    {
        var error = Assert.Throws<LevelLoadException>(() => Load($"""
            [niveau]
            etoiles = {value}

            [carte]
            #####
            #@.>#
            #####
            """));

        Assert.Contains("etoiles", Assert.Single(error.Issues).Message);
    }

    [Fact]
    public void FirstVictoryCreatesEveryRecord()
    {
        var book = new RecordBook();

        var update = book.Submit(Level, new LevelResult(16, 2, 2, 2));

        Assert.True(update.FirstCompletion);
        Assert.True(update.FewestTurns);
        Assert.Null(update.Previous);
        Assert.Equal(new LevelRecord(16, 2, 2, 2, 2, 2), book.Get(Level));
        Assert.True(book.IsCompleted(Level));
    }

    [Fact]
    public void EachRecordIsKeptIndependently()
    {
        var book = new RecordBook();
        book.Submit(Level, new LevelResult(18, 2, 2, 2));

        var update = book.Submit(Level, new LevelResult(14, 3, 7, 1));

        Assert.False(update.FirstCompletion);
        Assert.True(update.FewestTurns);
        Assert.True(update.MostStars);
        Assert.False(update.FewestTeleports);
        Assert.True(update.MostTeleports);
        Assert.True(update.FewestSwaps);
        Assert.False(update.MostSwaps);
        Assert.Equal(new LevelRecord(14, 3, 2, 7, 1, 2), book.Get(Level));

        var worse = book.Submit(Level, new LevelResult(30, 1, 0, 3));

        Assert.False(worse.FewestTurns);
        Assert.False(worse.MostStars);
        Assert.True(worse.FewestTeleports);
        Assert.True(worse.MostSwaps);
        Assert.Equal(new LevelRecord(14, 3, 0, 7, 1, 3), book.Get(Level));
    }

    [Fact]
    public void EqualResultIsNotARecord()
    {
        var book = new RecordBook();
        book.Submit(Level, new LevelResult(16, 2, 2, 2));

        var update = book.Submit(Level, new LevelResult(16, 2, 2, 2));

        Assert.False(update.AnyRecord);
    }

    [Fact]
    public void RecordsRoundTripThroughJson()
    {
        var book = new RecordBook();
        book.Submit(Level, new LevelResult(18, 2, 2, 2));
        book.Submit(Level, new LevelResult(14, 3, 7, 1));
        book.Submit("niveau_02.txt", new LevelResult(30, 1, 0, 0));

        var parsed = RecordCodec.Parse("records.json", RecordCodec.Serialize(book));

        Assert.Equal(book.Levels.OrderBy(entry => entry.Key), parsed.Levels.OrderBy(entry => entry.Key));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ cassé")]
    [InlineData("{\"format\":2,\"niveaux\":{}}")]
    [InlineData("{\"format\":1}")]
    [InlineData("{\"format\":1,\"niveaux\":{\"a.txt\":{\"tours\":5}}}")]
    [InlineData("{\"format\":1,\"niveaux\":{\"a.txt\":{\"tours\":5,\"etoiles\":4,\"spacio_magus_min\":0,\"spacio_magus_max\":0,\"spacio_creatura_min\":0,\"spacio_creatura_max\":0}}}")]
    public void InvalidRecordFilesAreRefused(string content)
    {
        Assert.Throws<LevelLoadException>(() => RecordCodec.Parse("records.json", content));
    }

    private static LoadedLevel Load(string content)
    {
        var legend = Legend.Parse("legende.txt", """
            [legende]
            # = mur
            . = sol
            > = escalier
            @ = depart
            """);
        return LevelFile.Parse("niveau_test.txt", content, legend, RuleSet.Default);
    }
}
