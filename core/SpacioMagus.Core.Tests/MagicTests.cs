namespace SpacioMagus.Core.Tests;

public class MagicTests
{
    private static readonly string[] Corridor =
    {
        "############",
        "#@........>#",
        "############",
    };

    [Fact]
    public void WaitingDoesNotRestoreMagic()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(Corridor));
        game.CastSpacioMagus(new Position(5, 1));
        Assert.Equal(3, game.Magus.Magic);

        var result = game.Wait();
        Assert.True(result.Success);
        Assert.Equal(3, game.Magus.Magic);
    }

    [Fact]
    public void PotionRestoresMagicToMaximum()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(
            "#########",
            "#@..p..>#",
            "#########"));

        game.CastSpacioMagus(new Position(4, 1));

        Assert.Equal(4, game.Magus.Magic);
        Assert.Empty(game.Potions);
    }

    [Fact]
    public void PotionStaysOnTheFloorWhenMagicIsFull()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(
            "######",
            "#@p.>#",
            "######"));

        game.Move(new Position(2, 1));

        Assert.Contains(new Position(2, 1), game.Potions);
    }

    [Fact]
    public void SpellIsRefusedWithoutEnoughMagic()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(Corridor), RuleSet.Default with { MagicMax = 1 });

        Assert.True(game.CastSpacioMagus(new Position(3, 1)).Success);
        Assert.Equal(ActionError.NotEnoughMagic, game.CastSpacioMagus(new Position(5, 1)).Error);
    }
}
