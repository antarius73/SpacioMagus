using SpacioMagus.Core.Loading;

namespace SpacioMagus.Core.Tests;

public class LoadingTests
{
    private const string LegendText = """
        [legende]
        ; commentaire
        # = mur
        . = sol
        G = grille
        P = porte
        > = escalier
        @ = depart
        b = blob_vert
        p = fiole
        r = blob_rouge
        * = brasero
        ~ = eau
        t = torche, decor
        B = bouclier, decor
        """;

    private static readonly Legend TestLegend = Legend.Parse("legende.txt", LegendText);

    private static LoadedLevel Load(string content) =>
        LevelFile.Parse("niveau_test.txt", content, TestLegend, RuleSet.Default);

    private static LevelLoadException LoadFails(string content) =>
        Assert.Throws<LevelLoadException>(() => Load(content));

    [Fact]
    public void WithoutMonstersKeyRemovesEveryMonster()
    {
        var level = Load("""
            [niveau]
            sans_monstres = oui

            [carte]
            #########
            ##..>..##
            #b.r...p#
            #...@...#
            #########
            """);

        Assert.Empty(level.Definition.Monsters);
        Assert.Equal(Terrain.Floor, level.Definition.TerrainAt(new Position(1, 2)));
    }

    [Fact]
    public void WithoutMonstersKeyExpectsYesOrNo()
    {
        var error = LoadFails("""
            [niveau]
            sans_monstres = peut-etre

            [carte]
            #########
            ##..>..##
            #b.....p#
            #...@...#
            #########
            """);

        Assert.Contains(error.Issues, issue => issue.Message.Contains("sans_monstres"));
    }

    [Fact]
    public void ValidLevelIsLoaded()
    {
        var level = Load("""
            [niveau]
            nom = Les cachots de Zorgus

            [carte]
            #########
            ##..>..##
            ####G####
            #b.....p#
            #...@...#
            #########

            [decor]
               t t
            """);

        var definition = level.Definition;
        Assert.Equal("Les cachots de Zorgus", definition.Name);
        Assert.Equal(9, definition.Width);
        Assert.Equal(6, definition.Height);
        Assert.Equal(new Position(4, 4), definition.MagusStart);
        Assert.Single(definition.Monsters);
        Assert.Equal(new Position(1, 3), definition.Monsters[0].Start);
        Assert.Equal(new[] { new Position(7, 3) }, definition.Potions);
        Assert.Equal(Terrain.Grille, definition.TerrainAt(new Position(4, 2)));
        Assert.Equal(Terrain.Stairs, definition.TerrainAt(new Position(4, 1)));
        Assert.Empty(level.Decor);
        Assert.Equal(new[] { new Position(3, 0), new Position(5, 0) }, definition.Torches);
    }

    [Fact]
    public void ShortLinesArePaddedWithVoid()
    {
        var level = Load("""
            [carte]
            #####
            #@>#
            #####
            """);

        Assert.Equal(5, level.Definition.Width);
        Assert.Equal(Terrain.Void, level.Definition.TerrainAt(new Position(4, 1)));
    }

    [Fact]
    public void UnknownCharacterIsReportedWithLineAndColumn()
    {
        var error = LoadFails("""
            [carte]
            #####
            #@?>#
            #####
            """);

        var issue = Assert.Single(error.Issues);
        Assert.Equal(3, issue.Line);
        Assert.Equal(3, issue.Column);
    }

    [Fact]
    public void DecorSymbolIsRefusedInTheMap()
    {
        var error = LoadFails("""
            [carte]
            #####
            #@t>#
            #####
            """);

        Assert.Contains("décor", Assert.Single(error.Issues).Message);
    }

    [Fact]
    public void MissingAndDuplicatedStartsAreReported()
    {
        Assert.Contains(LoadFails("""
            [carte]
            ####
            #.>#
            ####
            """).Issues, issue => issue.Message.Contains("aucun départ"));

        var duplicate = LoadFails("""
            [carte]
            #####
            #@@>#
            #####
            """);
        var issue = Assert.Single(duplicate.Issues);
        Assert.Equal(3, issue.Column);
    }

    [Fact]
    public void MissingStairsIsReported()
    {
        Assert.Contains(LoadFails("""
            [carte]
            ####
            #@.#
            ####
            """).Issues, issue => issue.Message.Contains("escalier"));
    }

    [Fact]
    public void MarkerGivesMonsterAPost()
    {
        var level = Load("""
            [carte]
            #######
            #@...>#
            #....1#
            #######

            [reperes]
            ; poste en colonne,ligne
            1 = blob_vert, poste = 2,2
            """);

        var spawn = Assert.Single(level.Definition.Monsters);
        Assert.Equal(new Position(5, 2), spawn.Start);
        Assert.Equal(new Position(2, 2), spawn.Post);
    }

    [Fact]
    public void UndefinedAndUnusedMarkersAreReported()
    {
        var error = LoadFails("""
            [carte]
            ######
            #@.2>#
            ######

            [reperes]
            1 = blob_vert
            """);

        Assert.Equal(2, error.Issues.Count);
        Assert.Contains(error.Issues, issue => issue.Message.Contains("« 2 »"));
        Assert.Contains(error.Issues, issue => issue.Message.Contains("« 1 »"));
    }

    [Fact]
    public void NewElementsAreLoaded()
    {
        var level = Load("""
            [carte]
            #######
            #@.r*~#
            #....>#
            #######
            """);

        var definition = level.Definition;
        Assert.Equal(MonsterKind.BlobRouge, Assert.Single(definition.Monsters).Kind);
        Assert.Equal(Terrain.Brazier, definition.TerrainAt(new Position(4, 1)));
        Assert.Equal(Terrain.Water, definition.TerrainAt(new Position(5, 1)));
    }

    [Fact]
    public void TorchMustHangOverFloor()
    {
        var error = LoadFails("""
            [carte]
            #######
            #######
            #@...>#
            #######

            [decor]
              t
            """);

        Assert.Contains("torchère", Assert.Single(error.Issues).Message);
    }

    [Fact]
    public void WallDecorMustHangOnAWallFace()
    {
        var level = Load("""
            [carte]
            #######
            #@...>#
            #######

            [decor]
             B
            """);
        Assert.Equal(new[] { new DecorItem(new Position(1, 0), "bouclier") }, level.Decor);

        var error = LoadFails("""
            [carte]
            #######
            #@...>#
            #######

            [decor]

              B
            """);
        Assert.Contains("bouclier", Assert.Single(error.Issues).Message);
    }

    [Fact]
    public void DecorLargerThanMapIsReported()
    {
        var error = LoadFails("""
            [carte]
            #####
            #@.>#
            #####

            [decor]
                  t
            """);

        Assert.Contains("hors de la carte", Assert.Single(error.Issues).Message);
    }

    [Fact]
    public void LevelCanOverrideRules()
    {
        var level = Load("""
            [niveau]
            portee_sort = 7

            [carte]
            #####
            #@.>#
            #####
            """);

        Assert.Equal(7, level.Rules.SpellRange);
        Assert.Equal(RuleSet.Default.MagicMax, level.Rules.MagicMax);
    }

    [Fact]
    public void DoorsAreGrillesWithTheirOwnLook()
    {
        var level = Load("""
            [carte]
            #########
            ##..>..##
            ####P####
            #.......#
            ####G####
            #...@...#
            #########
            """);

        var definition = level.Definition;
        Assert.Equal(Terrain.Grille, definition.TerrainAt(new Position(4, 2)));
        Assert.Equal(Terrain.Grille, definition.TerrainAt(new Position(4, 4)));
        Assert.True(definition.IsDoor(new Position(4, 2)));
        Assert.False(definition.IsDoor(new Position(4, 4)));
        Assert.Equal(new[] { new Position(4, 2) }, definition.Doors);
        Assert.Equal(2, definition.Grilles().Count());
    }

    [Fact]
    public void DoorOutsideAGrilleIsRejected()
    {
        var cells = new[]
        {
            Terrain.Wall, Terrain.Wall, Terrain.Wall,
            Terrain.Wall, Terrain.Floor, Terrain.Wall,
            Terrain.Wall, Terrain.Stairs, Terrain.Wall,
            Terrain.Wall, Terrain.Wall, Terrain.Wall,
        };

        Assert.Throws<LevelDefinitionException>(() => new LevelDefinition(
            "t", 3, 4, cells, new Position(1, 1), Array.Empty<MonsterSpawn>(), Array.Empty<Position>(), null, new[] { new Position(1, 2) }));
    }

    [Fact]
    public void BadGrilleIsReportedThroughLevelValidation()
    {
        var error = LoadFails("""
            [carte]
            #####
            #@G.#
            #.>.#
            #####
            """);

        Assert.Contains("grille", Assert.Single(error.Issues).Message);
    }

    [Fact]
    public void UnknownSectionIsReported()
    {
        var error = LoadFails("""
            [carte]
            #####
            #@.>#
            #####

            [monstres]
            """);

        Assert.Equal(6, Assert.Single(error.Issues).Line);
    }

    [Fact]
    public void SettingsAreParsedAndValidated()
    {
        var settings = GameSettings.Parse("regles.txt", """
            ; règles globales
            magie_max = 5
            duree_grille = 6
            """);

        Assert.Equal(5, settings.Rules.MagicMax);
        Assert.Equal(6, settings.Rules.GrilleOpenDuration);

        Assert.Throws<LevelLoadException>(() => GameSettings.Parse("regles.txt", "vitesse = 3"));
        Assert.Throws<LevelLoadException>(() => GameSettings.Parse("regles.txt", "magie_max = 0"));
        Assert.Throws<LevelLoadException>(() => GameSettings.Parse("regles.txt", "magie_max = beaucoup"));
    }

    [Fact]
    public void LegendRejectsReservedAndUnknownSymbols()
    {
        Assert.Throws<LevelLoadException>(() => Legend.Parse("legende.txt", "1 = mur"));
        Assert.Throws<LevelLoadException>(() => Legend.Parse("legende.txt", "x = dragon"));
        Assert.Throws<LevelLoadException>(() => Legend.Parse("legende.txt", "# = mur\n# = sol"));
    }

    [Fact]
    public void LegendSymbolsAreCaseSensitive()
    {
        var legend = Legend.Parse("legende.txt", "G = grille\ng = sol");

        Assert.True(legend.TryGet('G', out var upper));
        Assert.True(legend.TryGet('g', out var lower));
        Assert.Equal("grille", upper.Id);
        Assert.Equal("sol", lower.Id);
    }

    [Fact]
    public void LevelListKeepsOrderAndRejectsDuplicates()
    {
        var levels = LevelList.Parse("niveaux.txt", """
            ; ordre de jeu
            niveau_01.txt
            niveau_02.txt
            """);

        Assert.Equal(new[] { "niveau_01.txt", "niveau_02.txt" }, levels);
        Assert.Throws<LevelLoadException>(() => LevelList.Parse("niveaux.txt", "a.txt\na.txt"));
        Assert.Throws<LevelLoadException>(() => LevelList.Parse("niveaux.txt", "; vide"));
    }
}
