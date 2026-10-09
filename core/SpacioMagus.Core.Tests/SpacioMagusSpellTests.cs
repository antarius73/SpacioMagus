namespace SpacioMagus.Core.Tests;

public class SpacioMagusSpellTests
{
    [Fact]
    public void TeleportIsLimitedToSpellRange()
    {
        var level = TestMaps.Parse(
            "############",
            "#@........>#",
            "############");

        Assert.Equal(ActionError.OutOfRange, TestMaps.NewGame(level).CastSpacioMagus(new Position(10, 1)).Error);

        var game = TestMaps.NewGame(level);
        Assert.True(game.CastSpacioMagus(new Position(9, 1)).Success);
        Assert.Equal(new Position(9, 1), game.Magus.Position);
        Assert.Equal(3, game.Magus.Magic);
    }

    [Fact]
    public void TeleportNeedsLineOfSight()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(
            "#######",
            "#@.#..#",
            "#..#.>#",
            "#######"));

        Assert.Equal(ActionError.NotVisible, game.CastSpacioMagus(new Position(4, 1)).Error);
        Assert.DoesNotContain(new Position(4, 1), game.TeleportTargets());
    }

    [Fact]
    public void ClosedGrilleBlocksMagicAndOpenGrilleLetsItThrough()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(
            "#####",
            "#..>#",
            "##G##",
            "#.@.#",
            "#####"));

        Assert.Equal(ActionError.NotVisible, game.CastSpacioMagus(new Position(2, 1)).Error);

        Assert.True(game.OpenGrille(new Position(2, 2)).Success);
        Assert.True(game.CastSpacioMagus(new Position(2, 1)).Success);
        Assert.Equal(new Position(2, 1), game.Magus.Position);
    }

    [Fact]
    public void TeleportMakesMonstersLoseTrack()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(
            "#######",
            "#b...@#",
            "#####.#",
            "#>....#",
            "#######"));
        var blob = game.Monsters[0];

        game.Wait();
        Assert.True(blob.IsTracking);
        Assert.Equal(new Position(2, 1), blob.Position);

        var result = game.CastSpacioMagus(new Position(5, 3));

        Assert.True(result.Success);
        Assert.Contains(result.Events, e => e is MonsterLostTrack);
        Assert.False(blob.IsTracking);
        Assert.Equal(new Position(1, 1), blob.Position);
    }

    [Fact]
    public void TeleportInSightKeepsMonsterTracking()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(
            "#########",
            "#b.....@#",
            "#.......#",
            "#>......#",
            "#########"));
        var blob = game.Monsters[0];

        game.Wait();
        Assert.True(blob.IsTracking);
        var before = blob.Position;

        var result = game.CastSpacioMagus(new Position(5, 3));

        Assert.True(result.Success);
        Assert.DoesNotContain(result.Events, e => e is MonsterLostTrack);
        Assert.True(blob.IsTracking);
        Assert.True(blob.Position.ChebyshevTo(game.Magus.Position) < before.ChebyshevTo(game.Magus.Position));
    }

    [Fact]
    public void LineOfSightFollowsTheExactLineBetweenCellCentres()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(
            "#########",
            "###>..###",
            "#.....###",
            "###...###",
            "###..@###",
            "#########"));
        var alcoveMouth = new Position(2, 2);
        var alcoveBack = new Position(1, 2);
        var arrival = new Position(5, 4);

        Assert.True(game.HasLineOfSight(alcoveMouth, arrival));
        Assert.True(game.HasLineOfSight(arrival, alcoveMouth));
        Assert.False(game.HasLineOfSight(alcoveBack, arrival));
        Assert.False(game.HasLineOfSight(arrival, alcoveBack));
    }

    [Fact]
    public void LineOfSightPassesACornerUnlessBothSidesBlock()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(
            "#####",
            "#@#>#",
            "##..#",
            "#...#",
            "#####"));

        Assert.False(game.HasLineOfSight(new Position(1, 1), new Position(2, 2)));
        Assert.False(game.HasLineOfSight(new Position(2, 2), new Position(1, 1)));
        Assert.True(game.HasLineOfSight(new Position(1, 3), new Position(2, 2)));
        Assert.True(game.HasLineOfSight(new Position(2, 2), new Position(1, 3)));
    }

    [Fact]
    public void MonsterKeepsChasingAfterTeleportIntoItsSight()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(
            "#########",
            "###.@.###",
            "#b....###",
            "###...###",
            "###...###",
            "###.>.###",
            "#########"));
        var blob = game.Monsters[0];

        game.Move(new Position(3, 2));
        Assert.True(blob.IsTracking);
        Assert.Equal(new Position(2, 2), blob.Position);

        var result = game.CastSpacioMagus(new Position(5, 4));

        Assert.True(result.Success);
        Assert.True(blob.IsTracking);
        Assert.DoesNotContain(result.Events, e => e is MonsterLostTrack);
        Assert.NotEqual(new Position(1, 2), blob.Position);
        Assert.Equal(new Position(3, 2), blob.Position);
    }
}
