namespace SpacioMagus.Core.Tests;

public class MonsterTests
{
    [Fact]
    public void MonsterChasesThenCapturesOnlyWhenAlreadyAdjacent()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(
            "######",
            "#@..b#",
            "#>...#",
            "######"));
        var blob = game.Monsters[0];

        game.Wait();
        Assert.True(blob.IsTracking);
        Assert.Equal(2, blob.Position.ChebyshevTo(game.Magus.Position));

        game.Wait();
        Assert.Equal(GameStatus.Playing, game.Status);
        Assert.Equal(1, blob.Position.ChebyshevTo(game.Magus.Position));

        var result = game.Wait();
        Assert.Equal(GameStatus.Lost, game.Status);
        Assert.Contains(result.Events, e => e is MagusCaptured);
    }

    [Fact]
    public void MonsterWaitsInFrontOfClosedGrille()
    {
        var level = TestMaps.WithPost(
            TestMaps.Parse(
                "#######",
                "#..#..#",
                "##G#..#",
                "#.b#.@#",
                "#..#.>#",
                "#######"),
            new Position(2, 3),
            new Position(2, 1));
        var game = TestMaps.NewGame(level);
        var blob = game.Monsters[0];

        game.Wait();
        game.Wait();

        Assert.Equal(new Position(2, 3), blob.Position);
        Assert.False(blob.IsTracking);
    }

    [Fact]
    public void MonsterKeepsTrackingWhenMagusWalksOutOfSight()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(
            "########",
            "#b....@#",
            "######.#",
            "#>.....#",
            "########"));
        var blob = game.Monsters[0];

        game.Wait();
        Assert.True(blob.IsTracking);

        game.Move(new Position(6, 2));
        game.Move(new Position(6, 3));

        Assert.True(blob.IsTracking);
        Assert.Equal(new Position(4, 1), blob.Position);
    }

    [Fact]
    public void MonsterNeverLosesTrackWhileMagusWalks()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(
            "############",
            "#b........@#",
            "#########..#",
            "#>.........#",
            "############"));
        var blob = game.Monsters[0];
        game.Wait();
        Assert.True(blob.IsTracking);

        var path = new[]
        {
            new Position(10, 2), new Position(10, 3), new Position(9, 3), new Position(8, 3),
            new Position(7, 3), new Position(6, 3), new Position(5, 3), new Position(4, 3),
        };
        foreach (var step in path)
        {
            if (game.Status != GameStatus.Playing)
            {
                break;
            }

            var result = game.Move(step);
            Assert.True(result.Success);
            Assert.DoesNotContain(result.Events, e => e is MonsterLostTrack);
            Assert.True(blob.IsTracking);
        }
    }
}
