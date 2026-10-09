namespace SpacioMagus.Core.Tests;

public class LevelDefinitionTests
{
    [Fact]
    public void LevelWithoutStairsIsRejected()
    {
        Assert.Throws<LevelDefinitionException>(() => TestMaps.Parse(
            "####",
            "#@.#",
            "####"));
    }

    [Fact]
    public void GrilleMustSitBetweenTwoWalls()
    {
        Assert.Throws<LevelDefinitionException>(() => TestMaps.Parse(
            "#####",
            "#@G.#",
            "#.>.#",
            "#####"));
    }

    [Fact]
    public void GrilleSidesFollowTheWallOrientation()
    {
        var level = TestMaps.Parse(
            "#####",
            "#..>#",
            "##G##",
            "#.@.#",
            "#####");

        Assert.Equal((new Position(2, 1), new Position(2, 3)), level.GrilleSides(new Position(2, 2)));
    }
}
