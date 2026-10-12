using SpacioMagus.Core.Loading;
using SpacioMagus.Core.Saving;

namespace SpacioMagus.Core.Tests;

public class ContainerTests
{
    private static readonly string[] Storeroom =
    {
        "#######",
        "#@U.k.#",
        "#.....#",
        "#....>#",
        "#######",
    };

    [Fact]
    public void ContainersCannotBeEnteredNorTeleportedOnto()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(Storeroom));

        Assert.Equal(ActionError.Blocked, game.Move(new Position(2, 1)).Error);
        Assert.Equal(ActionError.Blocked, game.CastSpacioMagus(new Position(4, 1)).Error);
        Assert.True(game.HasLineOfSight(new Position(1, 1), new Position(5, 1)));
        Assert.Empty(game.Potions);
    }

    [Fact]
    public void BreakingAnAdjacentBarrelCostsATurnAndRevealsThePotion()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(Storeroom));

        var result = game.Break(new Position(2, 1));

        Assert.True(result.Success);
        Assert.Equal(1, game.Turn);
        Assert.Contains(result.Events, e => e is ContainerBroken { HadPotion: true });
        Assert.False(game.IsContainer(new Position(2, 1)));
        Assert.Contains(new Position(2, 1), game.Potions);
    }

    [Fact]
    public void RevealedPotionIsDrunkByWalkingOnIt()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(Storeroom));
        Assert.True(game.CastSpacioMagus(new Position(1, 3)).Success);
        Assert.True(game.Move(new Position(1, 2)).Success);
        Assert.True(game.Break(new Position(2, 1)).Success);

        Assert.True(game.Move(new Position(2, 1)).Success);

        Assert.Empty(game.Potions);
        Assert.Equal(game.Rules.MagicMax, game.Magus.Magic);
    }

    [Fact]
    public void BreakingDiagonallyIsAllowed()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(Storeroom));
        game.Move(new Position(1, 2));

        Assert.True(game.Break(new Position(2, 1)).Success);
    }

    [Fact]
    public void DistantContainerCannotBeBroken()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(Storeroom));

        Assert.Equal(ActionError.NotAdjacent, game.Break(new Position(4, 1)).Error);
        Assert.Equal(0, game.Turn);
    }

    [Fact]
    public void EmptyContainerLeavesNothing()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(Storeroom));
        game.Move(new Position(2, 2));
        game.Move(new Position(3, 2));

        var result = game.Break(new Position(4, 1));

        Assert.Contains(result.Events, e => e is ContainerBroken { HadPotion: false });
        Assert.Empty(game.Potions);
        Assert.True(game.IsWalkable(new Position(4, 1)));
    }

    [Fact]
    public void OnlyContainersCanBeBroken()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(Storeroom));

        Assert.Equal(ActionError.NotAContainer, game.Break(new Position(1, 2)).Error);
    }

    [Fact]
    public void BlobsWalkAroundContainers()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(
            "#######",
            "#@.k.b#",
            "#.....#",
            "#....>#",
            "#######"));
        var blob = game.Monsters[0];

        game.Wait();
        game.Wait();

        Assert.NotEqual(new Position(3, 1), blob.Position);
        Assert.Equal(2, blob.Position.ChebyshevTo(new Position(1, 1)));
        Assert.Equal(GameStatus.Playing, game.Status);
    }

    [Fact]
    public void ContainersAreLoadedFromTheMap()
    {
        var legend = Legend.Parse("legende.txt", """
            [legende]
            # = mur
            . = sol
            > = escalier
            @ = depart
            u = tonneau
            U = tonneau_fiole
            k = caisse
            K = caisse_fiole
            """);

        var level = LevelFile.Parse("niveau_test.txt", """
            [carte]
            #######
            #@uUkK#
            #....>#
            #######
            """, legend, RuleSet.Default).Definition;

        Assert.Equal(Terrain.Barrel, level.TerrainAt(new Position(2, 1)));
        Assert.Equal(Terrain.Crate, level.TerrainAt(new Position(5, 1)));
        Assert.Equal(new[] { new Position(3, 1), new Position(5, 1) }, level.Potions);
    }

    [Fact]
    public void ReplayReproducesABrokenContainer()
    {
        var journal = new GameJournal(TestMaps.Parse(Storeroom), RuleSet.Default, 4);
        journal.Play(new BreakAction(new Position(2, 1)));
        journal.Play(new MoveAction(new Position(2, 2)));

        var save = SaveCodec.Parse("sauvegarde.json", SaveCodec.Serialize(SaveGame.InProgress("niveau_test.txt", "empreinte", journal)));
        var restored = SaveRestorer.Restore(save, TestMaps.Parse(Storeroom), RuleSet.Default, "empreinte");

        Assert.False(restored.FellBack);
        Assert.False(restored.Journal.Game.IsContainer(new Position(2, 1)));
        Assert.Contains(new Position(2, 1), restored.Journal.Game.Potions);
    }
}
