using SpacioMagus.Core.Saving;

namespace SpacioMagus.Core.Tests;

public class CloneTests
{
    private static readonly string[] Map =
    {
        "#########",
        "#>..b...#",
        "####G####",
        "#.U.@.b.#",
        "#.......#",
        "#########",
    };

    [Fact]
    public void CloneContinuesExactlyLikeTheOriginal()
    {
        var original = TestMaps.NewGame(TestMaps.Parse(Map));
        Assert.True(original.Move(new Position(3, 3)).Success);
        Assert.True(original.Break(new Position(2, 3)).Success);
        Assert.True(original.OpenGrille(new Position(4, 2)).Success);

        var copy = original.Clone(new FixedRandom());
        var actions = new GameAction[] { new WaitAction(), new MoveAction(new Position(4, 4)), new WaitAction(), new WaitAction() };
        foreach (var action in actions)
        {
            Assert.Equal(original.Apply(action).Success, copy.Apply(action).Success);
        }

        Assert.Equal(GameDigest.Compute(original), GameDigest.Compute(copy));
        Assert.Equal(original.Turn, copy.Turn);
    }

    [Fact]
    public void PlayingOnTheCloneLeavesTheOriginalUntouched()
    {
        var original = TestMaps.NewGame(TestMaps.Parse(Map));
        var before = GameDigest.Compute(original);

        var copy = original.Clone(new FixedRandom());
        copy.OpenGrille(new Position(4, 2));
        copy.Move(new Position(3, 4));

        Assert.Equal(before, GameDigest.Compute(original));
        Assert.NotEqual(before, GameDigest.Compute(copy));
    }
}
