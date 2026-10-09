namespace SpacioMagus.Core.Tests;

public class MovementTests
{
    private static readonly string[] Room =
    {
        "#####",
        "#@..#",
        "#...#",
        "#..>#",
        "#####",
    };

    [Fact]
    public void MoveToAdjacentFloorSucceedsAndConsumesTurn()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(Room));

        var result = game.Move(new Position(2, 2));

        Assert.True(result.Success);
        Assert.Equal(new Position(2, 2), game.Magus.Position);
        Assert.Equal(1, game.Turn);
    }

    [Fact]
    public void MoveIntoWallIsBlockedWithoutConsumingTurn()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(Room));

        var result = game.Move(new Position(0, 1));

        Assert.Equal(ActionError.Blocked, result.Error);
        Assert.Equal(0, game.Turn);
    }

    [Fact]
    public void MoveFartherThanOneCellIsRejected()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(Room));

        Assert.Equal(ActionError.NotAdjacent, game.Move(new Position(3, 1)).Error);
    }

    [Fact]
    public void DiagonalMoveAcrossWallCornerIsBlocked()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(
            "#####",
            "#@#.#",
            "##..#",
            "#..>#",
            "#####"));

        Assert.Equal(ActionError.CornerBlocked, game.Move(new Position(2, 2)).Error);
    }

    [Fact]
    public void MagusCannotMoveOntoMonster()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(
            "#####",
            "#@b>#",
            "#####"));

        Assert.Equal(ActionError.OccupiedByMonster, game.Move(new Position(2, 1)).Error);
    }

    [Fact]
    public void ReachingStairsWinsTheLevel()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(Room));

        game.Move(new Position(2, 2));
        var result = game.Move(new Position(3, 3));

        Assert.Equal(GameStatus.Won, game.Status);
        Assert.Contains(result.Events, e => e is LevelCompleted);
        Assert.Equal(ActionError.GameOver, game.Wait().Error);
    }
}
