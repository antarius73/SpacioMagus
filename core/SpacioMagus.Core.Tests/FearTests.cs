using SpacioMagus.Core.Saving;

namespace SpacioMagus.Core.Tests;

public class FearTests
{
    private static readonly string[] MagusUnderTorch =
    {
        "###T###",
        "#..@..#",
        "#..b..#",
        "#....>#",
        "#######",
    };

    [Fact]
    public void FireZoneCoversThreeCellsUnderTorchAndNineAroundBrazier()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(
            "###T####",
            "#......#",
            "#......#",
            "#.*..@.#",
            "#......#",
            "#>.....#",
            "########"));

        var expected = new HashSet<Position> { new(2, 1), new(3, 1), new(4, 1) };
        for (var y = 2; y <= 4; y++)
        {
            for (var x = 1; x <= 3; x++)
            {
                expected.Add(new Position(x, y));
            }
        }

        Assert.True(expected.SetEquals(game.FearZone(MonsterKind.BlobVert)));
        Assert.Empty(game.FearZone(MonsterKind.BlobRouge));
    }

    [Fact]
    public void BrazierBlocksWalkingButNotSight()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(
            "#######",
            "#@*...#",
            "#....>#",
            "#######"));

        Assert.False(game.IsWalkable(new Position(2, 1)));
        Assert.True(game.HasLineOfSight(new Position(1, 1), new Position(3, 1)));
        Assert.Equal(ActionError.Blocked, game.CastSpacioMagus(new Position(2, 1)).Error);
        Assert.Equal(ActionError.Blocked, game.Move(new Position(2, 1)).Error);
    }

    [Fact]
    public void DiagonalMovesMayBrushPastABrazier()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(
            "#####",
            "#.*.#",
            "#@..#",
            "#..>#",
            "#####"));

        Assert.True(game.Move(new Position(1, 1)).Success);
        Assert.True(game.Move(new Position(2, 2)).Success);
        Assert.True(game.IsCornerFree(new Position(2, 2), new Position(3, 1)));
    }

    [Fact]
    public void WaterZoneCoversItsNeighboursAndScaresOnlyRedBlobs()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(
            "######",
            "#.~..#",
            "#@..>#",
            "######"));

        var expected = new HashSet<Position> { new(1, 1), new(3, 1), new(1, 2), new(2, 2), new(3, 2) };
        Assert.True(expected.SetEquals(game.FearZone(MonsterKind.BlobRouge)));
        Assert.False(game.IsFeared(MonsterKind.BlobVert, new Position(1, 1)));
        Assert.False(game.IsWalkable(new Position(2, 1)));
    }

    [Fact]
    public void BlockedBlobWaitsThenMutatesThenAttacks()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(MagusUnderTorch));
        var blob = game.Monsters[0];

        var first = game.Wait();
        Assert.Contains(first.Events, e => e is MonsterAfraid);
        Assert.Equal(new Position(3, 2), blob.Position);
        Assert.Equal(MonsterKind.BlobVert, blob.Kind);

        var second = game.Wait();
        Assert.Contains(second.Events, e => e is MonsterMutationStarted);
        Assert.True(blob.IsMutating);
        Assert.Equal(MonsterKind.BlobVert, blob.Kind);

        var third = game.Wait();
        Assert.Contains(third.Events, e => e is MonsterMutated { To: MonsterKind.BlobRouge });
        Assert.Equal(MonsterKind.BlobRouge, blob.Kind);
        Assert.Equal(GameStatus.Playing, game.Status);

        game.Wait();
        Assert.Equal(GameStatus.Lost, game.Status);
    }

    [Fact]
    public void LeavingTheFearBeforeMutationResetsTheCounter()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(
            "###T####",
            "#..@...#",
            "#..b...#",
            "#.....>#",
            "########"));
        var blob = game.Monsters[0];

        game.Wait();
        Assert.Equal(1, blob.FearTurns);

        game.CastSpacioMagus(new Position(6, 1));
        Assert.Equal(0, blob.FearTurns);
        Assert.False(blob.IsMutating);
    }

    [Fact]
    public void BlobApproachesAsCloseAsFearAllowsThenMutates()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(
            "########",
            "#..*...#",
            "#..@..b#",
            "#>######"));
        var blob = game.Monsters[0];

        game.Wait();
        Assert.Equal(new Position(5, 2), blob.Position);

        var blocked = game.Wait();
        Assert.Contains(blocked.Events, e => e is MonsterAfraid);
        Assert.Equal(new Position(5, 2), blob.Position);

        game.Wait();
        game.Wait();
        Assert.Equal(MonsterKind.BlobRouge, blob.Kind);
        Assert.Equal(GameStatus.Playing, game.Status);

        game.Wait();
        game.Wait();
        Assert.Equal(GameStatus.Lost, game.Status);
    }

    [Fact]
    public void FrightenedBlobFleesInsteadOfAttackingThenAttacksFromSafety()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(
            "####T##",
            "#.@b..#",
            "#.....#",
            "#....>#",
            "#######"));
        var blob = game.Monsters[0];

        game.Wait();
        Assert.Equal(GameStatus.Playing, game.Status);
        Assert.Equal(new Position(3, 2), blob.Position);

        game.Wait();
        Assert.Equal(GameStatus.Lost, game.Status);
    }

    [Fact]
    public void CorneredFrightenedBlobMutates()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(
            "##T####",
            "#.b.#@#",
            "#####>#",
            "#######"));
        var blob = game.Monsters[0];

        game.Wait();
        Assert.Equal(1, blob.FearTurns);
        game.Wait();
        Assert.True(blob.IsMutating);
        game.Wait();
        Assert.Equal(MonsterKind.BlobRouge, blob.Kind);
        Assert.Equal(new Position(2, 1), blob.Position);
    }

    [Fact]
    public void StunPausesTheMutation()
    {
        var game = TestMaps.NewGame(TestMaps.Parse(
            "###T###",
            "#..@..#",
            "#b.b..#",
            "#....>#",
            "#######"));
        var mutating = game.Monsters.Single(monster => monster.Position == new Position(3, 2));

        game.Wait();
        game.Wait();
        Assert.True(mutating.IsMutating);

        Assert.True(game.CastSpacioCreatura(new Position(3, 2), new Position(2, 2)).Success);
        Assert.True(mutating.IsStunned);
        Assert.True(mutating.IsMutating);

        for (var turn = 0; turn < 3; turn++)
        {
            game.Wait();
        }

        Assert.False(mutating.IsStunned);
        Assert.Equal(MonsterKind.BlobVert, mutating.Kind);

        game.Wait();
        Assert.Equal(MonsterKind.BlobRouge, mutating.Kind);
        Assert.Equal(GameStatus.Playing, game.Status);
    }

    [Fact]
    public void TorchMustHangOverFloor()
    {
        Assert.Throws<LevelDefinitionException>(() => TestMaps.Parse(
            "##T##",
            "#####",
            "#@.>#",
            "#####"));
    }

    [Fact]
    public void ReplayReproducesAMutation()
    {
        var journal = new GameJournal(TestMaps.Parse(MagusUnderTorch), RuleSet.Default, 9);
        for (var turn = 0; turn < 3; turn++)
        {
            journal.Play(new WaitAction());
        }

        var save = SaveGame.InProgress("niveau_test.txt", "empreinte", journal);
        var restored = SaveRestorer.Restore(save, TestMaps.Parse(MagusUnderTorch), RuleSet.Default, "empreinte");

        Assert.False(restored.FellBack);
        Assert.Equal(MonsterKind.BlobRouge, restored.Journal.Game.Monsters[0].Kind);
        Assert.Equal(GameDigest.Compute(journal.Game), GameDigest.Compute(restored.Journal.Game));
    }
}
