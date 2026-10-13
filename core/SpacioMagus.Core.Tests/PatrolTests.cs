using SpacioMagus.Core.Loading;
using SpacioMagus.Core.Saving;

namespace SpacioMagus.Core.Tests;

public class PatrolTests
{
    private static readonly string[] Hall =
    {
        "#########",
        "#.......#",
        "#b.....b#",
        "#.......#",
        "#########",
        "#@.....>#",
        "#########",
    };

    private static readonly Position[] Waypoints =
    {
        new(1, 2), new(2, 2), new(3, 1), new(5, 1), new(6, 2), new(7, 2), new(6, 2), new(5, 3), new(3, 3), new(2, 2),
    };

    private static readonly Position[] Loop =
    {
        new(1, 2), new(2, 2), new(3, 1), new(4, 1), new(5, 1), new(6, 2),
        new(7, 2), new(6, 2), new(5, 3), new(4, 3), new(3, 3), new(2, 2),
    };

    [Fact]
    public void WaypointsExpandIntoAClosedLoopOfNeighbours()
    {
        Assert.Equal(Loop, PatrolRoute.Expand(Waypoints));
        Assert.Equal(new[] { new Position(2, 3) }, PatrolRoute.Expand(new[] { new Position(2, 3) }));
    }

    [Fact]
    public void PeriodIsTheLeastCommonMultipleOfTheLoops()
    {
        Assert.Equal(48, PatrolRoute.Period(new[] { 12, 16, 1 }));
    }

    [Fact]
    public void TwoBlobsOnTheSameLoopFollowTheScheduleWithoutMeeting()
    {
        var game = NewHall(new Position(1, 2), new Position(7, 2));
        var left = game.Monsters.Single(monster => monster.Position == new Position(1, 2));
        var right = game.Monsters.Single(monster => monster.Position == new Position(7, 2));

        for (var turn = 1; turn <= 24; turn++)
        {
            game.Wait();
            Assert.Equal(Loop[turn % Loop.Length], left.Position);
            Assert.Equal(Loop[(turn + 6) % Loop.Length], right.Position);
            Assert.NotEqual(left.Position, right.Position);
        }
    }

    [Fact]
    public void BlobOffItsRoundRejoinsAndWaitsForItsSlot()
    {
        var level = TestMaps.Parse(
            "#########",
            "#.......#",
            "#...b...#",
            "#.......#",
            "#########",
            "#@.....>#",
            "#########");
        var game = TestMaps.NewGame(TestMaps.WithRoute(level, new Position(4, 2), Waypoints));
        var blob = game.Monsters[0];

        game.Wait();
        Assert.Equal(new Position(3, 1), blob.Position);
        game.Wait();
        Assert.Equal(new Position(3, 1), blob.Position);
        game.Wait();
        Assert.Equal(new Position(4, 1), blob.Position);

        for (var turn = 4; turn <= 20; turn++)
        {
            game.Wait();
            Assert.Equal(Loop[turn % Loop.Length], blob.Position);
        }
    }

    [Fact]
    public void BlobLeavesItsRoundToChaseTheMagus()
    {
        var level = TestMaps.Parse(
            "#######",
            "#b....#",
            "#.....#",
            "#..@.>#",
            "#######");
        var game = TestMaps.NewGame(TestMaps.WithRoute(level, new Position(1, 1), new Position(1, 1), new Position(5, 1)));
        var blob = game.Monsters[0];

        game.Wait();

        Assert.True(blob.IsTracking);
        Assert.Equal(new Position(2, 2), blob.Position);
    }

    [Fact]
    public void RouteThroughAWallIsRefused()
    {
        var level = TestMaps.Parse(Hall);

        Assert.Throws<LevelDefinitionException>(() => TestMaps.WithRoute(level, new Position(1, 2), new Position(1, 2), new Position(1, 5)));
    }

    [Fact]
    public void RouteCuttingAWallCornerIsRefused()
    {
        var level = TestMaps.Parse(
            "######",
            "#b.#.#",
            "#...>#",
            "#@...#",
            "######");

        Assert.Throws<LevelDefinitionException>(() => TestMaps.WithRoute(level, new Position(1, 1), new Position(2, 1), new Position(3, 2), new Position(2, 2)));
    }

    [Fact]
    public void RoundIsReadFromTheLevelFile()
    {
        var legend = Legend.Parse("legende.txt", """
            [legende]
            # = mur
            . = sol
            > = escalier
            @ = depart
            """);

        var level = LevelFile.Parse("niveau_test.txt", """
            [carte]
            #########
            #.......#
            #1.....1#
            #.......#
            #########
            #@.....>#
            #########

            [reperes]
            1 = blob_vert, ronde = 1,2 2,2 3,1 5,1 6,2 7,2 6,2 5,3 3,3 2,2
            """, legend, RuleSet.Default).Definition;

        Assert.All(level.Monsters, spawn => Assert.Equal(Loop, spawn.Patrol));
    }

    [Fact]
    public void ReplayKeepsTheRoundsInStep()
    {
        var level = TestMaps.WithRoute(TestMaps.WithRoute(TestMaps.Parse(Hall), new Position(1, 2), Waypoints), new Position(7, 2), Waypoints);
        var journal = new GameJournal(level, RuleSet.Default, 3);
        for (var turn = 0; turn < 7; turn++)
        {
            journal.Play(new WaitAction());
        }

        var restored = SaveRestorer.Restore(SaveGame.InProgress("niveau_test.txt", "empreinte", journal), level, RuleSet.Default, "empreinte");

        Assert.False(restored.FellBack);
        Assert.Equal(GameDigest.Compute(journal.Game), GameDigest.Compute(restored.Journal.Game));
        Assert.Equal(12, restored.Journal.Game.PatrolPeriod);
    }

    private static Game NewHall(Position first, Position second)
    {
        var level = TestMaps.WithRoute(TestMaps.WithRoute(TestMaps.Parse(Hall), first, Waypoints), second, Waypoints);
        return TestMaps.NewGame(level);
    }
}
