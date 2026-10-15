using SpacioMagus.Core.Loading;

namespace SpacioMagus.Core.Tests;

public class StrandedTests
{
    private static readonly RuleSet OneSpell = RuleSet.Default with { MagicMax = 1 };

    private static Game JumpTo(Position target, RuleSet rules, params string[] map)
    {
        var game = TestMaps.NewGame(TestMaps.Parse(map), rules);
        Assert.True(game.CastSpacioMagus(target).Success);
        return game;
    }

    [Fact]
    public void IslandWithoutMagicIsLostAtTheNextTurn()
    {
        var game = JumpTo(new Position(3, 1), OneSpell,
            "#########",
            "#@~..~~>#",
            "#########");

        Assert.True(game.IsStranded);
        Assert.Equal(GameStatus.Playing, game.Status);

        var result = game.Apply(new WaitAction());

        Assert.True(result.Success);
        Assert.Equal(GameStatus.Lost, game.Status);
        Assert.Equal(LossReason.Stranded, game.LossReason);
        Assert.Contains(result.Events, e => e is MagusStranded);
    }

    [Fact]
    public void AnyActionAnnouncesTheDefeat()
    {
        var game = JumpTo(new Position(3, 1), OneSpell,
            "#########",
            "#@~..~~>#",
            "#########");

        game.Apply(new MoveAction(new Position(4, 1)));

        Assert.Equal(LossReason.Stranded, game.LossReason);
        Assert.Equal(new Position(3, 1), game.Magus.Position);
    }

    [Fact]
    public void PotionOnTheIslandKeepsHope()
    {
        var game = JumpTo(new Position(3, 1), OneSpell,
            "#########",
            "#@~.p~~>#",
            "#########");

        Assert.False(game.IsStranded);
    }

    [Fact]
    public void EvenAnEmptyContainerKeepsHope()
    {
        var game = JumpTo(new Position(3, 1), OneSpell,
            "##########",
            "#@~..u~~>#",
            "##########");

        Assert.False(game.IsStranded);
    }

    [Fact]
    public void ReachableExitIsNotStranded()
    {
        var game = JumpTo(new Position(3, 1), OneSpell,
            "########",
            "#@~...>#",
            "########");

        Assert.False(game.IsStranded);
    }

    [Fact]
    public void DoorTowardsTheExitIsNotStranded()
    {
        var game = JumpTo(new Position(3, 1), OneSpell,
            "#######",
            "#@~.#>#",
            "###.#.#",
            "###.G.#",
            "#######");

        Assert.False(game.IsStranded);
    }

    [Fact]
    public void WallTowardsTheExitIsStranded()
    {
        var game = JumpTo(new Position(3, 1), OneSpell,
            "#######",
            "#@~.#>#",
            "###.#.#",
            "###.#.#",
            "#######");

        Assert.True(game.IsStranded);
    }

    [Fact]
    public void ReachableLeverOfTheHerseIsNotStranded()
    {
        var legend = Legend.Parse("legende.txt", """
            [legende]
            # = mur
            . = sol
            > = escalier
            @ = depart
            ~ = eau
            G = grille
            m = levier
            """);
        var level = LevelFile.Parse("niveau_test.txt", """
            [carte]
            ########
            #@~..#>#
            ###m.#.#
            ####.G.#
            ########

            [manettes]
            3,2 = 5,3
            """, legend, OneSpell);
        var game = new Game(level.Definition, OneSpell, new FixedRandom());

        Assert.True(game.CastSpacioMagus(new Position(3, 1)).Success);

        Assert.False(game.IsStranded);
    }

    [Fact]
    public void MagicLeftMeansNoStranding()
    {
        var game = JumpTo(new Position(3, 1), RuleSet.Default,
            "#########",
            "#@~..~~>#",
            "#########");

        Assert.False(game.IsStranded);
    }
}
