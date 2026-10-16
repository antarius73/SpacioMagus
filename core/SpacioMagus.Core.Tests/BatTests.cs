using SpacioMagus.Core.Loading;

namespace SpacioMagus.Core.Tests;

public class BatTests
{
    private static readonly Legend TestLegend = Legend.Parse("legende.txt", """
        [legende]
        # = mur
        . = sol
        > = escalier
        @ = depart
        ~ = eau
        j = vase
        c = chauve_souris
        * = brasero
        """);

    private static LoadedLevel Load(string content) =>
        LevelFile.Parse("niveau_test.txt", content, TestLegend, RuleSet.Default);

    private static Game NewGame(string content, RuleSet? rules = null) =>
        new(Load(content).Definition, rules ?? RuleSet.Default, new FixedRandom());

    private static MonsterState Bat(Game game) =>
        game.Monsters.Single(monster => monster.Kind == MonsterKind.ChauveSouris);

    [Fact]
    public void BatPatrolsOverWaterAndContainers()
    {
        var game = NewGame("""
            [carte]
            #######
            #1~j..#
            #######
            #@...>#
            #######

            [reperes]
            1 = chauve_souris, ronde = 1,1 3,1
            """);

        Assert.True(game.Wait().Success);
        Assert.Equal(new Position(2, 1), Bat(game).Position);
        Assert.True(game.Wait().Success);
        Assert.Equal(new Position(3, 1), Bat(game).Position);
        Assert.False(Bat(game).IsTracking);
    }

    [Fact]
    public void BlobRouteStillNeedsFloor()
    {
        var error = Assert.Throws<LevelLoadException>(() => Load("""
            [carte]
            #######
            #1~...#
            #@...>#
            #######

            [reperes]
            1 = blob_vert, ronde = 1,1 3,1
            """));

        Assert.Contains(error.Issues, issue => issue.Message.Contains("n'est pas une case de sol"));
    }

    [Fact]
    public void BatRouteCannotCrossWalls()
    {
        var error = Assert.Throws<LevelLoadException>(() => Load("""
            [carte]
            #######
            #1#...#
            #@...>#
            #######

            [reperes]
            1 = chauve_souris, ronde = 1,1 3,1
            """));

        Assert.Contains(error.Issues, issue => issue.Message.Contains("survoler"));
    }

    [Fact]
    public void BatFliesThroughAnOpenGrille()
    {
        var map = TestMaps.Parse(
            "#######",
            "#..@.>#",
            "###G###",
            "#.....#",
            "#..u..#",
            "#######");
        var level = new LevelDefinition(
            map.Name, map.Width, map.Height, map.Cells, map.MagusStart,
            new[] { new MonsterSpawn(MonsterKind.ChauveSouris, new Position(3, 3), new Position(3, 3)) },
            map.Potions, map.Torches, map.Doors);
        var game = new Game(level, RuleSet.Default, new FixedRandom());

        Assert.True(game.OpenGrille(new Position(3, 2)).Success);
        var bitten = false;
        for (var turn = 0; turn < 3 && !bitten; turn++)
        {
            bitten = game.Wait().Events.Any(e => e is MagusBitten);
        }

        Assert.True(bitten);
    }

    [Fact]
    public void BatStopsAtAClosedGrille()
    {
        var map = TestMaps.Parse(
            "#######",
            "#..@.>#",
            "###G###",
            "#.....#",
            "#..u..#",
            "#######");
        var level = new LevelDefinition(
            map.Name, map.Width, map.Height, map.Cells, map.MagusStart,
            new[] { new MonsterSpawn(MonsterKind.ChauveSouris, new Position(3, 3), new Position(3, 3)) },
            map.Potions, map.Torches, map.Doors);
        var game = new Game(level, RuleSet.Default, new FixedRandom());

        for (var turn = 0; turn < 3; turn++)
        {
            Assert.DoesNotContain(game.Wait().Events, e => e is MagusBitten);
            Assert.Equal(3, game.Monsters.Single().Position.Y);
        }
    }

    private static readonly Position GatePosition = new(4, 3);

    private static Game BatLockedBehindAGrille()
    {
        var map = TestMaps.Parse(
            "#########",
            "#.......#",
            "#...@...#",
            "####G####",
            "#.......#",
            "#.......#",
            "#.......#",
            "#.......#",
            "#.......#",
            "#.......#",
            "#.......#",
            "#.......#",
            "#>......#",
            "#########");
        var level = new LevelDefinition(
            map.Name, map.Width, map.Height, map.Cells, map.MagusStart,
            new[] { new MonsterSpawn(MonsterKind.ChauveSouris, new Position(4, 11), new Position(4, 11)) },
            map.Potions, map.Torches, map.Doors);
        var game = new Game(level, RuleSet.Default, new FixedRandom());

        Assert.True(game.OpenGrille(GatePosition).Success);
        Assert.True(Bat(game).IsTracking);
        foreach (var step in new[] { new Position(5, 1), new Position(6, 1), new Position(7, 1) })
        {
            Assert.True(game.Move(step).Success);
        }

        for (var turn = 0; turn < 10 && game.GrilleAt(GatePosition)!.IsOpen; turn++)
        {
            Assert.True(game.Wait().Success);
        }

        Assert.False(game.GrilleAt(GatePosition)!.IsOpen);

        return game;
    }

    [Fact]
    public void BlockedBatHeadsForTheGrilleRatherThanTheWall()
    {
        var game = BatLockedBehindAGrille();

        for (var turn = 0; turn < 3; turn++)
        {
            Assert.True(game.Wait().Success);
        }

        Assert.Equal(new Position(4, 4), Bat(game).Position);
    }

    [Fact]
    public void BlockedBatWaitsInFrontOfTheClosedGrille()
    {
        var game = BatLockedBehindAGrille();
        for (var turn = 0; turn < 5 && Bat(game).Position != new Position(4, 4); turn++)
        {
            Assert.True(game.Wait().Success);
        }

        for (var turn = 0; turn < 3; turn++)
        {
            Assert.True(game.Wait().Success);
            Assert.Equal(new Position(4, 4), Bat(game).Position);
        }
    }

    [Fact]
    public void BatGoesThroughOnceTheGrilleOpens()
    {
        var game = BatLockedBehindAGrille();
        foreach (var step in new[] { new Position(6, 1), new Position(5, 1), new Position(4, 2) })
        {
            Assert.True(game.Move(step).Success);
        }

        Assert.True(game.OpenGrille(GatePosition).Success);
        var bitten = false;
        for (var turn = 0; turn < 3 && !bitten; turn++)
        {
            bitten = game.Wait().Events.Any(e => e is MagusBitten);
        }

        Assert.True(bitten);
    }

    [Fact]
    public void BatNeverLosesTrack()
    {
        var game = NewGame("""
            [carte]
            #########
            #@.....c#
            #.#######
            #.......#
            #....>..#
            #########
            """);

        Assert.True(game.Wait().Success);
        Assert.True(Bat(game).IsTracking);
        Assert.True(game.CastSpacioMagus(new Position(1, 3)).Success);
        Assert.False(game.HasLineOfSight(Bat(game).Position, game.Magus.Position));
        Assert.True(Bat(game).IsTracking);
    }

    [Fact]
    public void BatWithoutPathStillGetsAsCloseAsPossible()
    {
        var game = NewGame("""
            [carte]
            #######
            #@...>#
            #*****#
            #..c..#
            #######
            """);

        Assert.True(game.Wait().Success);
        Assert.True(Bat(game).IsTracking);
        Assert.Equal(new Position(2, 3), Bat(game).Position);
        Assert.True(game.Wait().Success);
        Assert.Equal(new Position(1, 3), Bat(game).Position);
        Assert.True(game.Wait().Success);
        Assert.Equal(new Position(1, 3), Bat(game).Position);
    }

    [Fact]
    public void BiteDrainsOneMagicThenTheBatRestsTwoTurns()
    {
        var game = NewGame("""
            [carte]
            ######
            #@c.>#
            ######
            """);
        var magic = game.Magus.Magic;

        var bite = game.Wait();
        Assert.Contains(bite.Events, e => e is MagusBitten);
        Assert.Equal(magic - 1, game.Magus.Magic);
        Assert.True(Bat(game).IsStunned);

        Assert.True(game.Wait().Success);
        Assert.True(game.Wait().Success);
        Assert.Equal(magic - 1, game.Magus.Magic);

        Assert.True(game.Wait().Success);
        Assert.Equal(magic - 2, game.Magus.Magic);
        Assert.Equal(new Position(2, 1), Bat(game).Position);
    }

    [Fact]
    public void MagusFaintsWhenBittenWithoutMagic()
    {
        var game = NewGame("""
            [carte]
            ######
            #@c.>#
            ######
            """, RuleSet.Default with { MagicMax = 1 });

        Assert.True(game.Wait().Success);
        Assert.Equal(0, game.Magus.Magic);
        Assert.True(game.Wait().Success);
        Assert.True(game.Wait().Success);
        var faint = game.Wait();

        Assert.Contains(faint.Events, e => e is MagusFainted);
        Assert.Equal(GameStatus.Lost, game.Status);
        Assert.Equal(LossReason.Fainted, game.LossReason);
    }

    [Fact]
    public void BatOverAContainerBlocksBreakingIt()
    {
        var game = NewGame("""
            [carte]
            ######
            #@#1.#
            #.j.>#
            ######

            [reperes]
            1 = chauve_souris, ronde = 3,1 3,2 2,2 3,2
            """);

        Assert.True(game.Wait().Success);
        Assert.True(game.Wait().Success);
        Assert.Equal(new Position(2, 2), Bat(game).Position);

        Assert.Equal(ActionError.OccupiedByMonster, game.Break(new Position(2, 2)).Error);
    }

    [Fact]
    public void SwapCannotDropABlobIntoWater()
    {
        var game = NewGame("""
            [carte]
            ########
            #@~c...#
            #......#
            #....>2#
            ########

            [reperes]
            2 = blob_vert
            """);

        Assert.True(game.Wait().Success);
        var bat = Bat(game);
        var blob = game.Monsters.Single(monster => monster.Kind == MonsterKind.BlobVert);
        Assert.Equal(new Position(2, 1), bat.Position);

        Assert.Equal(ActionError.SwapBlocked, game.CastSpacioCreatura(bat.Position, blob.Position).Error);
    }
}
