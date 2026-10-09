namespace SpacioMagus.Core.Tests;

public class GrilleTests
{
    private static readonly string[] Gate =
    {
        "#####",
        "#..>#",
        "##G##",
        "#.@.#",
        "#####",
    };

    private static readonly Position GrillePosition = new(2, 2);

    [Fact]
    public void OpeningGrilleCostsTurnAndStartsCounter()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(Gate));

        var result = game.OpenGrille(GrillePosition);

        Assert.True(result.Success);
        Assert.Equal(1, game.Turn);
        Assert.Equal(game.Rules.GrilleOpenDuration, game.GrilleAt(GrillePosition)!.TurnsRemaining);
    }

    [Fact]
    public void OpenGrilleCannotBeClosedAndTouchingItIsFree()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(Gate));
        game.OpenGrille(GrillePosition);

        Assert.Equal(ActionError.GrilleAlreadyOpen, game.OpenGrille(GrillePosition).Error);
        Assert.Equal(1, game.Turn);
    }

    [Fact]
    public void GrilleStaysOpenForFiveActionsThenCloses()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(Gate));
        var grille = game.GrilleAt(GrillePosition)!;
        game.OpenGrille(GrillePosition);

        for (var i = 0; i < 4; i++)
        {
            game.Wait();
        }

        Assert.True(grille.IsOpen);
        Assert.Equal(1, grille.TurnsRemaining);

        var result = game.Wait();

        Assert.False(grille.IsOpen);
        Assert.Contains(result.Events, e => e is GrilleClosed);
    }

    [Fact]
    public void TouchingSomethingElseIsNotAGrille()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(Gate));

        Assert.Equal(ActionError.NotAGrille, game.OpenGrille(new Position(1, 3)).Error);
    }

    [Fact]
    public void ClosingGrillePushesMagusToAFreeSide()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(Gate));
        var grille = game.GrilleAt(GrillePosition)!;
        game.OpenGrille(GrillePosition);
        Assert.True(game.Move(GrillePosition).Success);

        game.Wait();
        game.Wait();
        game.Wait();
        Assert.Equal(GrillePosition, game.Magus.Position);

        game.Wait();

        Assert.False(grille.IsOpen);
        Assert.Equal(new Position(2, 1), game.Magus.Position);
    }
}
