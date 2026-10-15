using SpacioMagus.Core.Loading;
using SpacioMagus.Core.Saving;

namespace SpacioMagus.Core.Tests;

public class NewElementsTests
{
    private static readonly Legend TestLegend = Legend.Parse("legende.txt", """
        [legende]
        # = mur
        . = sol
        > = escalier
        @ = depart
        ~ = eau
        G = grille
        P = porte
        j = vase
        J = vase_fiole
        m = levier
        c = chauve_souris
        e = escalier_montant
        a = arche
        t = torche, decor
        A = alcove, decor
        """);

    private const string LeverLevel = """
        [carte]
        #######
        #.@m..#
        ###G###
        #..>..#
        #######

        [manettes]
        3,1 = 3,2
        """;

    private static LoadedLevel Load(string content) =>
        LevelFile.Parse("niveau_test.txt", content, TestLegend, RuleSet.Default);

    private static Game NewGame(string content) =>
        new(Load(content).Definition, RuleSet.Default, new FixedRandom());

    private static LevelLoadException LoadFails(string content) =>
        Assert.Throws<LevelLoadException>(() => Load(content));

    [Fact]
    public void Level2ElementsAreLoaded()
    {
        var level = Load("""
            [carte]
            #########
            #@.mj..>#
            #.J.1..e#
            ####G####
            ####.####
            ##.a.####
            #########

            [reperes]
            1 = chauve_souris

            [manettes]
            3,1 = 4,3

            [decor]
             A
            """);

        var definition = level.Definition;
        Assert.Equal(Terrain.Lever, definition.TerrainAt(new Position(3, 1)));
        Assert.Equal(Terrain.Vase, definition.TerrainAt(new Position(4, 1)));
        Assert.Equal(Terrain.Vase, definition.TerrainAt(new Position(2, 2)));
        Assert.Equal(Terrain.StairsUp, definition.TerrainAt(new Position(7, 2)));
        Assert.Equal(Terrain.Arch, definition.TerrainAt(new Position(3, 5)));
        Assert.Equal(new[] { new Position(2, 2) }, definition.Potions);
        var bat = Assert.Single(definition.Monsters);
        Assert.Equal(MonsterKind.ChauveSouris, bat.Kind);
        Assert.Equal(new Position(4, 2), bat.Start);
        Assert.Equal(new[] { new Position(1, 0) }, definition.Alcoves);
        Assert.Equal(new Position(4, 3), definition.LeverTarget(new Position(3, 1)));
        Assert.Empty(level.Decor);
    }

    [Fact]
    public void StationaryBatIsLoadedFromTheLegend()
    {
        var level = Load("""
            [carte]
            #######
            #@.c.>#
            #######
            """);

        var bat = Assert.Single(level.Definition.Monsters);
        Assert.Equal(MonsterKind.ChauveSouris, bat.Kind);
        Assert.Equal(new Position(3, 1), bat.Post);
    }

    [Fact]
    public void LeverCannotBeEnteredNorTeleportedOnto()
    {
        var game = NewGame(LeverLevel);

        Assert.Equal(ActionError.Blocked, game.Move(new Position(3, 1)).Error);
        Assert.Equal(ActionError.Blocked, game.CastSpacioMagus(new Position(3, 1)).Error);
        Assert.True(game.HasLineOfSight(new Position(2, 1), new Position(5, 1)));
    }

    [Fact]
    public void LeverOpensItsHerseForTheGrilleDuration()
    {
        var game = NewGame(LeverLevel);
        var herse = new Position(3, 2);

        var pulled = game.PullLever(new Position(3, 1));

        Assert.True(pulled.Success);
        Assert.Contains(pulled.Events, e => e is LeverPulled { Lever.X: 3, Grille.Y: 2 });
        Assert.Contains(pulled.Events, e => e is GrilleOpened);
        for (var turn = 1; turn < game.Rules.GrilleOpenDuration; turn++)
        {
            Assert.True(game.Wait().Success);
            Assert.True(game.GrilleAt(herse)!.IsOpen, $"Herse refermée trop tôt (tour {turn}).");
        }

        var closing = game.Wait();
        Assert.False(game.GrilleAt(herse)!.IsOpen);
        Assert.Contains(closing.Events, e => e is GrilleClosed);
    }

    [Fact]
    public void LeverIsLockedUntilItsHerseFalls()
    {
        var game = NewGame(LeverLevel);
        var herse = new Position(3, 2);
        Assert.True(game.PullLever(new Position(3, 1)).Success);
        Assert.True(game.Wait().Success);
        var remaining = game.GrilleAt(herse)!.TurnsRemaining;

        Assert.Equal(ActionError.GrilleAlreadyOpen, game.PullLever(new Position(3, 1)).Error);
        Assert.Equal(remaining, game.GrilleAt(herse)!.TurnsRemaining);

        while (game.GrilleAt(herse)!.IsOpen)
        {
            Assert.True(game.Wait().Success);
        }

        Assert.True(game.PullLever(new Position(3, 1)).Success);
    }

    [Fact]
    public void HerseCannotBeOpenedByHand()
    {
        var game = NewGame(LeverLevel);

        Assert.Equal(ActionError.GrilleNeedsLever, game.OpenGrille(new Position(3, 2)).Error);
    }

    [Fact]
    public void LeverMustBeAdjacent()
    {
        var game = NewGame("""
            [carte]
            #######
            #@..m.#
            ###G###
            #..>..#
            #######

            [manettes]
            4,1 = 3,2
            """);

        Assert.Equal(ActionError.NotAdjacent, game.PullLever(new Position(4, 1)).Error);
        Assert.Equal(ActionError.NotALever, game.PullLever(new Position(2, 1)).Error);
    }

    [Fact]
    public void DoorsStillOpenByHand()
    {
        var game = NewGame("""
            [carte]
            #######
            #.@...#
            ###P###
            #..>..#
            #######
            """);

        Assert.True(game.OpenGrille(new Position(3, 2)).Success);
    }

    [Fact]
    public void LeverWithoutHerseIsRejected()
    {
        var error = LoadFails("""
            [carte]
            #######
            #@.m..#
            ###G###
            #..>..#
            #######
            """);

        Assert.Contains("manette", Assert.Single(error.Issues).Message);
    }

    [Fact]
    public void LeverCannotCommandADoor()
    {
        var error = LoadFails("""
            [carte]
            #######
            #@.m..#
            ###P###
            #..>..#
            #######

            [manettes]
            3,1 = 3,2
            """);

        Assert.Contains("herse", Assert.Single(error.Issues).Message);
    }

    [Fact]
    public void LeverLinesMustBeCoordinates()
    {
        var error = LoadFails(LeverLevel + "\nmanette = 3,2\n");

        Assert.Contains("x,y = x,y", error.Issues[0].Message);
    }

    [Fact]
    public void LeverActionSurvivesASave()
    {
        var journal = new GameJournal(Load(LeverLevel).Definition, RuleSet.Default, 3);
        Assert.True(journal.Play(new LeverAction(new Position(3, 1))).Success);

        var save = SaveGame.InProgress("niveau_test.txt", "empreinte", journal);
        var parsed = SaveCodec.Parse("sauvegarde.json", SaveCodec.Serialize(save));

        Assert.Equal(new GameAction[] { new LeverAction(new Position(3, 1)) }, parsed.Actions);
    }

    [Fact]
    public void ArchIsAnOpenPassage()
    {
        var game = NewGame("""
            [carte]
            #####
            #@..#
            ##a##
            #..>#
            #####
            """);

        Assert.True(game.Move(new Position(2, 1)).Success);
        Assert.True(game.Move(new Position(2, 2)).Success);
        Assert.True(game.HasLineOfSight(new Position(2, 1), new Position(2, 3)));
    }

    [Fact]
    public void ArchMustStandBetweenTwoWalls()
    {
        var error = LoadFails("""
            [carte]
            #####
            #@a.#
            #..>#
            #####
            """);

        Assert.Contains("arche", Assert.Single(error.Issues).Message);
    }

    [Fact]
    public void StairsUpBlockTheWayButNotTheSight()
    {
        var game = NewGame("""
            [carte]
            #######
            #@e..>#
            #######
            """);

        Assert.Equal(ActionError.Blocked, game.Move(new Position(2, 1)).Error);
        Assert.True(game.HasLineOfSight(new Position(1, 1), new Position(4, 1)));
    }

    [Fact]
    public void BatWithoutRouteStaysUntilItSeesTheMagus()
    {
        var game = NewGame("""
            [carte]
            #########
            #@#.....#
            #.#.c..>#
            #########
            """);

        Assert.True(game.Move(new Position(1, 2)).Success);
        Assert.True(game.Move(new Position(1, 1)).Success);
        for (var turn = 0; turn < 6; turn++)
        {
            Assert.True(game.Wait().Success);
        }

        var bat = Assert.Single(game.Monsters);
        Assert.Equal(GameStatus.Playing, game.Status);
        Assert.Equal(new Position(4, 2), bat.Position);
        Assert.False(bat.IsTracking);
    }

    [Fact]
    public void BatFearsNothingAndNeverMutates()
    {
        var game = NewGame("""
            [carte]
            #########
            #@#..c..#
            ###~~~.>#
            #########

            [decor]
                 t
            """);

        Assert.Empty(game.FearZone(MonsterKind.ChauveSouris));
        for (var turn = 0; turn < 6; turn++)
        {
            Assert.True(game.Wait().Success);
        }

        var bat = Assert.Single(game.Monsters);
        Assert.Equal(MonsterKind.ChauveSouris, bat.Kind);
        Assert.False(bat.IsMutating);
        Assert.Equal(0, bat.FearTurns);
    }

    [Fact]
    public void AlcoveMustHangOnAWallFace()
    {
        var error = LoadFails("""
            [carte]
            #######
            #######
            #@...>#
            #######

            [decor]
              A
            """);

        Assert.Contains("alcôve", Assert.Single(error.Issues).Message);
    }
}
