using SpacioMagus.Core.Loading;
using SpacioMagus.Core.Saving;

namespace SpacioMagus.Core.Tests;

public class TreasureTests
{
    private static readonly Legend TestLegend = Legend.Parse("legende.txt", """
        [legende]
        # = mur
        . = sol
        > = escalier
        @ = depart
        b = blob_vert
        c = chauve_souris
        u = tonneau
        C = coffre
        Q = cle
        P = porte
        L = porte_verrouillee
        ^ = piege
        T = autel
        m = levier
        G = grille
        """);

    private static Game NewGame(string map, string extra = "", string script = "", int seed = 0)
    {
        var loaded = LevelFile.Parse("niveau_test.txt", $"[niveau]\n{script}\n[carte]\n{map}\n{extra}", TestLegend, RuleSet.Default);
        return new Game(loaded.Definition, loaded.Rules, new SeededRandom((ulong)seed));
    }

    private const string AltarRoom = """
        #########
        #.......#
        #.TT.C..#
        #.@..C.u#
        #.......#
        #.b.....#
        #######>#
        #########
        """;

    [Fact]
    public void ChestIsAnUnbreakableObstacleThatOpensAndCloses()
    {
        var game = NewGame("""
            #####
            #@C>#
            #####
            """);
        var chest = new Position(2, 1);
        var open = game.IsChestOpen(chest);

        Assert.Equal(ActionError.Blocked, game.Move(chest).Error);
        Assert.Equal(ActionError.NotAContainer, game.Break(chest).Error);
        var result = game.Apply(new ChestAction(chest));

        Assert.True(result.Success);
        Assert.Equal(1, game.Turn);
        Assert.Equal(!open, game.IsChestOpen(chest));
        Assert.Contains(result.Events, e => e is ChestToggled toggled && toggled.Open == !open);
    }

    [Fact]
    public void ChestsStartOpenOrClosedAtRandom()
    {
        var states = new HashSet<bool>();
        for (var seed = 1; seed <= 20; seed++)
        {
            states.Add(NewGame("""
                #####
                #@C>#
                #####
                """, seed: seed).IsChestOpen(new Position(2, 1)));
        }

        Assert.Equal(2, states.Count);
    }

    [Fact]
    public void LockedDoorWithoutTheKeyIsRefused()
    {
        var game = NewGame("""
            #####
            #@#>#
            #L#.#
            #...#
            #####
            """);

        Assert.Equal(ActionError.DoorLocked, game.OpenGrille(new Position(1, 2)).Error);
        Assert.Equal(0, game.Turn);
    }

    [Fact]
    public void SpikesRiseEveryFourthTurnAndBlockTheWay()
    {
        var game = NewGame("""
            ######
            #@^.>#
            ######
            """);
        var spikes = new Position(2, 1);

        Assert.False(game.SpikesRaised(spikes));
        game.Wait();
        game.Wait();
        Assert.False(game.SpikesRaised(spikes));
        var rising = game.Wait();
        Assert.True(game.SpikesRaised(spikes));
        Assert.Contains(rising.Events, e => e is SpikesRaised);
        Assert.Equal(ActionError.Blocked, game.Move(spikes).Error);
        var lowering = game.Wait();
        Assert.False(game.SpikesRaised(spikes));
        Assert.Contains(lowering.Events, e => e is SpikesLowered);
        Assert.True(game.Move(spikes).Success);
    }

    [Fact]
    public void SpikesRisingUnderTheMagusKillHim()
    {
        var game = NewGame("""
            ######
            #@^.>#
            ######
            """);

        game.Wait();
        game.Wait();
        var result = game.Move(new Position(2, 1));

        Assert.Equal(GameStatus.Lost, game.Status);
        Assert.Equal(LossReason.Impaled, game.LossReason);
        Assert.Contains(result.Events, e => e is MagusImpaled);
    }

    [Fact]
    public void SpikeOffsetShiftsTheCycle()
    {
        var game = NewGame("""
            ######
            #@^.>#
            ######
            """, "[pieges]\n2,1 = 3\n");

        Assert.True(game.SpikesRaised(new Position(2, 1)));
        Assert.Equal(ActionError.Blocked, game.Move(new Position(2, 1)).Error);
    }

    [Fact]
    public void ScrollIsTakenFromANeighbourCellAndUnlocksObjectSwaps()
    {
        var game = NewGame(AltarRoom, script: "script = parchemin_creatura");

        Assert.Single(game.SwapTargets());
        Assert.Contains(new Position(2, 2), game.Activables());
        var result = game.Activate(new Position(2, 2));

        Assert.True(result.Success);
        Assert.Equal(1, game.Turn);
        Assert.True(game.CanSwapObjects);
        Assert.Contains(result.Events, e => e is ScrollTaken);
        Assert.Empty(game.Activables());
        Assert.Equal(ActionError.AlreadyActive, game.Activate(new Position(3, 2)).Error);
        Assert.Contains(new Position(5, 2), game.SwapTargets());
    }

    [Fact]
    public void AltarIsAnObstacle()
    {
        var game = NewGame(AltarRoom, script: "script = parchemin_creatura");

        Assert.Equal(ActionError.Blocked, game.Move(new Position(2, 2)).Error);
    }

    [Fact]
    public void ImprovedSwapMovesChestsAndMonsters()
    {
        var game = NewGame(AltarRoom, script: "script = parchemin_creatura");
        game.Activate(new Position(2, 2));
        var chest = new Position(5, 3);
        var blob = game.Monsters.Single();
        var blobCell = blob.Position;
        var open = game.IsChestOpen(chest);

        var result = game.CastSpacioCreatura(chest, blobCell);

        Assert.True(result.Success);
        Assert.True(game.IsChest(blobCell));
        Assert.Equal(open, game.IsChestOpen(blobCell));
        Assert.False(game.IsChest(chest));
        Assert.Equal(chest, blob.Position);
        Assert.True(blob.StunTurns > 0);
        Assert.Contains(result.Events, e => e is ObjectMoved moved && moved.From == chest && moved.To == blobCell);
        Assert.True(game.IsWalkable(new Position(5, 2)) == false);
    }

    [Fact]
    public void ImprovedSwapExchangesTwoObjectsAndKeepsTheirContents()
    {
        var game = Build("parchemin_creatura", "##########", "#.TT.C...#", "#.@......#", "#.....u..#", "#######>##", "##########");
        game.Activate(new Position(2, 1));
        var chest = new Position(5, 1);
        var barrel = new Position(6, 3);

        var result = game.CastSpacioCreatura(chest, barrel);

        Assert.True(result.Success);
        Assert.True(game.IsChest(barrel));
        Assert.True(game.IsContainer(chest));
        Assert.Equal(Terrain.Barrel, game.ObjectKind(chest));
        Assert.Equal(Terrain.Chest, game.ObjectKind(barrel));
    }

    [Fact]
    public void WithoutTheScrollObjectsAreNotSwappable()
    {
        var game = NewGame(AltarRoom, script: "script = parchemin_creatura");

        Assert.Equal(ActionError.NotEnoughCreatures, game.CastSpacioCreatura(new Position(5, 2), new Position(5, 3)).Error);
    }

    [Fact]
    public void ChestActionSurvivesASave()
    {
        var save = new SaveGame(Game.RulesVersion, "niveau_03.txt", null, 3, new GameAction[] { new ChestAction(new Position(2, 5)) }, null);

        var parsed = SaveCodec.Parse("sauvegarde.json", SaveCodec.Serialize(save));

        Assert.Equal(new ChestAction(new Position(2, 5)), Assert.Single(parsed.Actions));
    }

    private static Game Build(string script, params string[] rows)
    {
        var height = rows.Length;
        var width = rows.Max(row => row.Length);
        var cells = new Terrain[width * height];
        var monsters = new List<MonsterSpawn>();
        var start = new Position(0, 0);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var symbol = x < rows[y].Length ? rows[y][x] : '#';
                var position = new Position(x, y);
                cells[y * width + x] = symbol switch
                {
                    '#' => Terrain.Wall,
                    '>' => Terrain.Stairs,
                    'C' => Terrain.Chest,
                    'T' => Terrain.Altar,
                    'u' => Terrain.Barrel,
                    '^' or 'B' or 'X' => Terrain.Spikes,
                    _ => Terrain.Floor,
                };
                if (symbol == '@')
                {
                    start = position;
                }

                if (symbol is 'b' or 'B')
                {
                    monsters.Add(new MonsterSpawn(MonsterKind.BlobVert, position, position));
                }

                if (symbol is 'c' or 'X')
                {
                    monsters.Add(new MonsterSpawn(MonsterKind.ChauveSouris, position, position));
                }
            }
        }

        var level = new LevelDefinition("test", width, height, cells, start, monsters, Array.Empty<Position>(), script: script.Length == 0 ? null : script);
        return new Game(level, RuleSet.Default, new SeededRandom(0));
    }

    [Fact]
    public void LockedDoorNeedsTheKeyOnceThenStaysUnlocked()
    {
        var game = NewGame("""
            #######
            #Q..@.#
            ####L##
            #>....#
            #######
            """);
        var door = new Position(4, 2);

        Assert.Equal(ActionError.DoorLocked, game.OpenGrille(door).Error);
        game.Move(new Position(3, 1));
        game.Move(new Position(2, 1));
        var picked = game.Move(new Position(1, 1));
        Assert.Contains(picked.Events, e => e is KeyPicked);
        Assert.Equal(1, game.KeysHeld);
        Assert.Empty(game.Keys);
        game.Move(new Position(2, 1));
        game.Move(new Position(3, 1));

        var opened = game.OpenGrille(door);

        Assert.True(opened.Success);
        Assert.Contains(opened.Events, e => e is DoorUnlocked);
        Assert.Equal(0, game.KeysHeld);
        Assert.False(game.IsLocked(door));
        for (var turn = 0; turn < 10 && game.GrilleAt(door)!.IsOpen; turn++)
        {
            game.Wait();
        }

        Assert.False(game.GrilleAt(door)!.IsOpen);
        Assert.True(game.OpenGrille(door).Success);
    }

    [Fact]
    public void ReachableKeyKeepsTheMagusFromBeingStranded()
    {
        var withKey = NewGame("""
            #######
            #Q..@.#
            ####L##
            #>....#
            #######
            """, "", "magie_max = 1\ncout_sort = 2");
        var withoutKey = NewGame("""
            #######
            #...@.#
            ####L##
            #>....#
            #######
            """, "", "magie_max = 1\ncout_sort = 2");

        withKey.Wait();
        withoutKey.Wait();

        Assert.False(withKey.IsStranded);
        Assert.True(withoutKey.IsStranded);
    }

    [Fact]
    public void SpikesStunABlobUntilTheEndOfTheFirstLoweredTurn()
    {
        var game = Build("", "########", "#@######", "#.###B.#", "#>######", "########");
        var blob = game.Monsters.Single();

        game.Wait();
        game.Wait();
        var rising = game.Wait();
        Assert.Contains(rising.Events, e => e is MonsterStunned { Turns: 2 });
        game.Wait();
        Assert.Equal(1, blob.StunTurns);
        var recovering = game.Wait();
        Assert.Equal(0, blob.StunTurns);
        Assert.Contains(recovering.Events, e => e is MonsterRecovered);
        Assert.False(game.SpikesRaised(blob.Position));
    }

    [Fact]
    public void SpikesDoNotHurtBats()
    {
        var game = Build("", "########", "#@######", "#.###X.#", "#>######", "########");
        var bat = game.Monsters.Single();

        game.Wait();
        game.Wait();
        var rising = game.Wait();

        Assert.Contains(rising.Events, e => e is SpikesRaised);
        Assert.Equal(0, bat.StunTurns);
    }

    [Fact]
    public void ObjectsCanLandOnSpikesWhichKeepCycling()
    {
        var game = Build("parchemin_creatura", "#########", "#.TT.TTT#", "#.@..TBT#", "#.C..TTT#", "#######>#", "#########");
        game.Activate(new Position(2, 1));
        var spikes = new Position(6, 2);

        var result = game.CastSpacioCreatura(new Position(2, 3), spikes);

        Assert.True(result.Success);
        Assert.True(game.IsChest(spikes));
        game.Wait();
        Assert.True(game.SpikesRaised(spikes));
        Assert.True(game.IsChest(spikes));
        Assert.Equal(GameStatus.Playing, game.Status);
    }

    private const string TwinLevers = """
        #########
        #...>...#
        #.......#
        ####G####
        #m.....m#
        #...@...#
        #########
        """;

    [Fact]
    public void GrilleWithTwoLeversOpensOnlyWhenBothArePulled()
    {
        var game = NewGame(TwinLevers, "[manettes]\n1,4 = 4,3\n7,4 = 4,3\n");
        var grille = new Position(4, 3);
        game.Move(new Position(3, 5));
        game.Move(new Position(2, 5));

        var first = game.PullLever(new Position(1, 4));

        Assert.True(first.Success);
        Assert.True(game.IsLeverPulled(new Position(1, 4)));
        Assert.False(game.GrilleAt(grille)!.IsOpen);
        Assert.Equal(ActionError.LeverAlreadyPulled, game.PullLever(new Position(1, 4)).Error);
        game.Move(new Position(3, 5));
        game.Move(new Position(4, 5));
        game.Move(new Position(5, 5));
        game.Move(new Position(6, 5));

        var second = game.PullLever(new Position(7, 4));

        Assert.True(second.Success);
        Assert.True(game.GrilleAt(grille)!.IsOpen);
        Assert.Contains(second.Events, e => e is GrilleOpened);
    }

    [Fact]
    public void BothLeversResetWhenTheGrilleCloses()
    {
        var game = NewGame(TwinLevers, "[manettes]\n1,4 = 4,3\n7,4 = 4,3\n");
        var grille = new Position(4, 3);
        game.Move(new Position(3, 5));
        game.Move(new Position(2, 5));
        game.PullLever(new Position(1, 4));
        foreach (var step in new[] { 3, 4, 5, 6 })
        {
            game.Move(new Position(step, 5));
        }

        game.PullLever(new Position(7, 4));
        var reset = new List<GameEvent>();
        for (var turn = 0; turn < 10 && game.GrilleAt(grille)!.IsOpen; turn++)
        {
            reset.AddRange(game.Wait().Events);
        }

        Assert.False(game.GrilleAt(grille)!.IsOpen);
        Assert.False(game.IsLeverPulled(new Position(1, 4)));
        Assert.False(game.IsLeverPulled(new Position(7, 4)));
        Assert.Equal(2, reset.OfType<LeverReset>().Count());
    }

    [Fact]
    public void SingleLeverStillOpensItsGrilleAtOnce()
    {
        var game = NewGame(TwinLevers.Replace("m.....m", "m......"), "[manettes]\n1,4 = 4,3\n");
        game.Move(new Position(3, 5));
        game.Move(new Position(2, 5));

        Assert.True(game.PullLever(new Position(1, 4)).Success);
        Assert.True(game.GrilleAt(new Position(4, 3))!.IsOpen);
        Assert.False(game.IsLeverPulled(new Position(1, 4)));
    }

    [Fact]
    public void ChestsBlockTheSpellLineButNotTheLitZone()
    {
        var game = NewGame("""
            #########
            #@.C...>#
            #########
            """);
        var behind = new Position(4, 1);

        Assert.Equal(ActionError.NotVisible, game.CastSpacioMagus(behind).Error);
        Assert.DoesNotContain(behind, game.TeleportTargets());
        Assert.Contains(behind, game.SightZone());
        Assert.True(game.HasLineOfSight(game.Magus.Position, behind));
    }

    [Fact]
    public void RaisedSpikesStayInTheLitZoneWithoutBeingTeleportTargets()
    {
        var game = NewGame("""
            #######
            #@.^.>#
            #######
            """, "[pieges]\n3,1 = 3\n");
        var spikes = new Position(3, 1);

        Assert.True(game.SpikesRaised(spikes));
        Assert.DoesNotContain(spikes, game.TeleportTargets());
        Assert.Contains(spikes, game.SightZone());
        Assert.Contains(new Position(4, 1), game.SightZone());
    }

    [Fact]
    public void ChestsBlockSpacioCreaturaTargetsBehindThem()
    {
        var game = Build("parchemin_creatura", "##########", "#.TT.....#", "#.@.C.b.b#", "#.......>#", "##########");
        game.Activate(new Position(2, 1));

        Assert.DoesNotContain(new Position(6, 2), game.SwapTargets());
        Assert.Contains(new Position(4, 2), game.SwapTargets());
    }
}
