namespace SpacioMagus.Core.Tests;

public class SpacioCreaturaSpellTests
{
    private static readonly string[] Hall =
    {
        "#######",
        "#@....#",
        "#.....#",
        "#b...b#",
        "#>....#",
        "#######",
    };

    private static readonly Position Left = new(1, 3);
    private static readonly Position Right = new(5, 3);

    [Fact]
    public void SwapExchangesTwoMonstersAndStunsThem()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(Hall));
        var first = game.Monsters[0];
        var second = game.Monsters[1];

        var result = game.CastSpacioCreatura(Left, Right);

        Assert.True(result.Success);
        Assert.Equal(Right, first.Position);
        Assert.Equal(Left, second.Position);
        Assert.Equal(game.Rules.StunDuration - 1, first.StunTurns);
        Assert.Equal(game.Rules.StunDuration - 1, second.StunTurns);
        Assert.Equal(3, game.Magus.Magic);
    }

    [Fact]
    public void StunnedMonstersStayStillForFourMonsterTurns()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(Hall));
        var second = game.Monsters[1];
        game.CastSpacioCreatura(Left, Right);

        game.Wait();
        game.Wait();
        game.Wait();
        Assert.Equal(Left, second.Position);
        Assert.False(second.IsStunned);

        game.Wait();
        Assert.Equal(1, second.Position.ChebyshevTo(game.Magus.Position));
        Assert.Equal(GameStatus.Playing, game.Status);
    }

    [Fact]
    public void MagusIsNotAValidTarget()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(Hall));

        Assert.Equal(ActionError.NotAMonster, game.CastSpacioCreatura(game.Magus.Position, Left).Error);
    }

    [Fact]
    public void SameCreatureCannotBeSwappedWithItself()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(Hall));

        Assert.Equal(ActionError.SameCreature, game.CastSpacioCreatura(Left, Left).Error);
    }

    [Fact]
    public void SwapNeedsTwoEligibleMonsters()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(
            "######",
            "#@...#",
            "#..b.#",
            "#>...#",
            "######"));

        Assert.Equal(ActionError.NotEnoughCreatures, game.CastSpacioCreatura(game.Magus.Position, new Position(3, 2)).Error);
        Assert.Equal(0, game.Turn);
    }

    [Fact]
    public void StunningAgainResetsTheCounterInsteadOfAddingTurns()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(Hall));
        var first = game.Monsters[0];
        game.CastSpacioCreatura(Left, Right);
        game.Wait();
        Assert.Equal(2, first.StunTurns);

        game.CastSpacioCreatura(Left, Right);

        Assert.Equal(game.Rules.StunDuration - 1, first.StunTurns);
    }
}
