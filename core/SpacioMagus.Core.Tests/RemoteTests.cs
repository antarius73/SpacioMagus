using SpacioMagus.Core.Remote;

namespace SpacioMagus.Core.Tests;

public class RemoteTests
{
    private static readonly string[] Storeroom =
    {
        "#######",
        "#@U.k.#",
        "#.....#",
        "#.b..>#",
        "###G###",
        "#.....#",
        "#######",
    };

    private static Game NewStoreroom() =>
        TestMaps.NewGame(TestMaps.WithRoute(TestMaps.Parse(Storeroom), new Position(2, 3), new Position(2, 3), new Position(3, 3)));

    [Fact]
    public void MapShowsMagusMonstersAndTerrainButNotHiddenPotions()
    {
        var view = PlayerView.Capture(NewStoreroom());

        Assert.Equal("#@u.k.#", view.Map[1]);
        Assert.Equal("#.b..>#", view.Map[3]);
        Assert.Equal("###P###", view.Map[4]);
    }

    [Fact]
    public void ContainerContentStaysHiddenUntilRevealed()
    {
        var view = PlayerView.Capture(NewStoreroom());

        Assert.All(view.Containers, container => Assert.Null(container.Potion));
        Assert.Equal(new[] { new Position(2, 1), new Position(4, 1) }, view.Containers.Select(container => container.Position));
    }

    [Fact]
    public void SerializedViewNeverExposesPatrolRoutesOrPosts()
    {
        var json = RemoteProtocol.WriteResponse(RemoteResponse.Success(1, RemoteScreen.Playing, PlayerView.Capture(NewStoreroom())));

        Assert.DoesNotContain("route", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("post", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fearTurns", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BrokenContainerBecomesFloorAndDroppedPotionIsShown()
    {
        var game = NewStoreroom();

        Assert.True(game.Break(new Position(2, 1)).Success);
        var view = PlayerView.Capture(game);

        Assert.Equal('!', view.Map[1][2]);
        Assert.Contains(new Position(2, 1), view.Potions);
        Assert.DoesNotContain(new Position(2, 1), view.Containers.Select(container => container.Position));
    }

    [Fact]
    public void LegalActionsMatchWhatTheGameAccepts()
    {
        var game = NewStoreroom();
        var legal = LegalActions.Compute(game);

        Assert.True(legal.Wait);
        Assert.Equal(new[] { new Position(1, 2), new Position(2, 2) }, legal.Move);
        Assert.Equal(new[] { new Position(2, 1) }, legal.Break);
        Assert.DoesNotContain(game.Magus.Position, legal.SpacioMagus);
        Assert.All(legal.Move, target => Assert.True(LegalActions.IsAccepted(game, new MoveAction(target))));
        Assert.All(legal.SpacioMagus, target => Assert.True(LegalActions.IsAccepted(game, new SpacioMagusAction(target))));
    }

    [Fact]
    public void ComputingLegalActionsDoesNotChangeTheGame()
    {
        var game = NewStoreroom();
        var before = RemoteProtocol.WriteResponse(RemoteResponse.Success(1, RemoteScreen.Playing, PlayerView.Capture(game)));

        LegalActions.Compute(game);
        var after = RemoteProtocol.WriteResponse(RemoteResponse.Success(1, RemoteScreen.Playing, PlayerView.Capture(game)));

        Assert.Equal(before, after);
        Assert.Equal(0, game.Turn);
    }

    [Fact]
    public void NoLegalActionOnceTheGameIsOver()
    {
        var game = TestMaps.NewGame(TestMaps.Parse("#####", "#@>.#", "#####"));

        Assert.True(game.Move(new Position(2, 1)).Success);

        Assert.Equal(GameStatus.Won, game.Status);
        Assert.Equal(LegalActionsView.None, LegalActions.Compute(game));
    }

    public static TheoryData<GameAction> AllActions() => new()
    {
        new MoveAction(new Position(1, 2)),
        new WaitAction(),
        new OpenGrilleAction(new Position(3, 4)),
        new SpacioMagusAction(new Position(5, 6)),
        new SpacioCreaturaAction(new Position(1, 1), new Position(2, 2)),
        new BreakAction(new Position(7, 8)),
        new LeverAction(new Position(9, 10)),
        new ActivateAction(new Position(11, 12)),
    };

    [Theory]
    [MemberData(nameof(AllActions))]
    public void PlayRequestRoundTripsEveryAction(GameAction action)
    {
        var line = RemoteProtocol.WriteRequest(new RemoteRequest(7, RemoteCommands.Play, 12, action));

        var request = RemoteProtocol.ReadRequest(line);

        Assert.Equal(new RemoteRequest(7, RemoteCommands.Play, 12, action), request);
        Assert.DoesNotContain('\n', line);
    }

    [Theory]
    [InlineData("pas du json", RemoteErrors.InvalidRequest)]
    [InlineData("[1, 2]", RemoteErrors.InvalidRequest)]
    [InlineData("{\"cmd\":\"state\"}", RemoteErrors.InvalidRequest)]
    [InlineData("{\"id\":1,\"cmd\":\"voler\"}", RemoteErrors.UnknownCommand)]
    [InlineData("{\"id\":1,\"cmd\":\"play\",\"turn\":0}", RemoteErrors.InvalidRequest)]
    [InlineData("{\"id\":1,\"cmd\":\"play\",\"action\":{\"type\":\"sauter\"}}", RemoteErrors.InvalidRequest)]
    [InlineData("{\"id\":1,\"cmd\":\"start\"}", RemoteErrors.InvalidRequest)]
    [InlineData("{\"id\":1,\"cmd\":\"ui\"}", RemoteErrors.InvalidRequest)]
    [InlineData("{\"id\":\"un\",\"cmd\":\"state\"}", RemoteErrors.InvalidRequest)]
    [InlineData("{\"id\":1,\"cmd\":\"play\",\"turn\":\"deux\",\"action\":{\"type\":\"attendre\"}}", RemoteErrors.InvalidRequest)]
    [InlineData("{\"id\":1,\"cmd\":\"ui\",\"control\":\"12\",\"value\":\"fort\"}", RemoteErrors.InvalidRequest)]
    public void MalformedRequestsAreRejectedWithACode(string line, string error)
    {
        var exception = Assert.Throws<RemoteProtocolException>(() => RemoteProtocol.ReadRequest(line));

        Assert.Equal(error, exception.Error);
    }

    [Fact]
    public void ResponseRoundTripsWithStateEventsAndLevels()
    {
        var game = NewStoreroom();
        var result = game.Move(new Position(2, 2));
        var response = new RemoteResponse(
            3,
            true,
            RemoteScreen.Playing,
            State: PlayerView.Capture(game),
            Events: EventText.Describe(result.Events, game),
            Levels: new[] { new RemoteLevel("niveau_01.txt", "Hall", true, 2, 15) });
        var line = RemoteProtocol.WriteResponse(response);

        var back = RemoteProtocol.ReadResponse(line);

        Assert.Equal(line, RemoteProtocol.WriteResponse(back));
        Assert.Contains("Le magus se déplace de (1,1) à (2,2).", back.Events!);
        Assert.DoesNotContain("\\u", line);
    }

    [Fact]
    public void RefusalCarriesTheGameError()
    {
        var line = RemoteProtocol.WriteResponse(new RemoteResponse(4, false, RemoteScreen.Playing, RemoteErrors.Refused, "Bloqué", ActionError.Blocked));

        var back = RemoteProtocol.ReadResponse(line);

        Assert.Equal(ActionError.Blocked, back.Refusal);
        Assert.Contains("\"refusal\":\"blocked\"", line);
    }

    [Fact]
    public void RenderAddsColumnAndRowAxes()
    {
        var text = MapText.Render(new[] { "#####", "#@.>#", "#####" });

        Assert.Equal("  01234\n0 #####\n1 #@.>#\n2 #####\n", text);
    }

    [Fact]
    public void RenderAddsTensRowForWideMaps()
    {
        var text = MapText.Render(new[] { new string('#', 12) });

        Assert.StartsWith("  0         1 \n  012345678901\n", text);
    }

    [Fact]
    public void UiRequestRoundTripsControlAndValue()
    {
        var line = RemoteProtocol.WriteRequest(new RemoteRequest(8, RemoteCommands.Ui, Control: "1234:2", Value: 70));

        Assert.Equal(new RemoteRequest(8, RemoteCommands.Ui, Control: "1234:2", Value: 70), RemoteProtocol.ReadRequest(line));
    }

    [Fact]
    public void UiViewRoundTripsInResponse()
    {
        var ui = new UiView(
            new[] { "Réglages" },
            new[]
            {
                new UiControl("10", UiKind.Choice, "Langue", true, new[] { "Français", "English" }, 0),
                new UiControl("11", UiKind.Slider, "Musique", true, Value: 80, Min: 0, Max: 100),
                new UiControl("12", UiKind.Button, "Retour", false),
            });
        var line = RemoteProtocol.WriteResponse(new RemoteResponse(9, true, RemoteScreen.Other, Ui: ui));

        var back = RemoteProtocol.ReadResponse(line);

        Assert.Equal(line, RemoteProtocol.WriteResponse(back));
        Assert.Equal(UiKind.Slider, back.Ui!.Controls[1].Kind);
        Assert.Contains("\"kind\":\"choice\"", line);
    }

    [Fact]
    public void FormattedStateListsMapCreaturesAndLegalActions()
    {
        var game = NewStoreroom();
        var text = ResponseText.Format(RemoteResponse.Success(1, RemoteScreen.Playing, PlayerView.Capture(game)));

        Assert.Contains("Écran : en jeu", text);
        Assert.Contains("Magus en (1,1), magie 4/4", text);
        Assert.Contains("\n1 #@u.k.#\n", text);
        Assert.Contains("- n°1 blob vert en (2,3)", text);
        Assert.Contains("- porte en (3,4) : fermée", text);
        Assert.Contains("- deplacer : (1,2) (2,2)", text);
        Assert.Contains("- briser : (2,1)", text);
        Assert.DoesNotContain("(fiole)", text);
    }

    [Fact]
    public void FormattedFailureStartsWithErrorCodeAndRefusal()
    {
        var text = ResponseText.Format(RemoteResponse.Refused(2, RemoteScreen.Playing, ActionError.NotAdjacent, null));

        Assert.StartsWith("ÉCHEC [action_refusee]", text);
        Assert.Contains("Refus du moteur : NotAdjacent", text);
    }

    [Fact]
    public void FormattedUiListsControlsWithIdsOptionsAndState()
    {
        var ui = new UiView(
            new[] { "Réglages" },
            new[]
            {
                new UiControl("10", UiKind.Choice, "Langue", true, new[] { "Français", "English" }, 0, 0, 1),
                new UiControl("11", UiKind.Slider, "Musique", true, Value: 80, Min: 0, Max: 100),
                new UiControl("12:1", UiKind.Door, "Niveau 2 (verrouillé)", false),
            });

        var text = ResponseText.Format(new RemoteResponse(3, true, RemoteScreen.Other, Ui: ui));

        Assert.Contains("- [10] liste « Langue » options : 0=Français, 1=English valeur actuelle 0", text);
        Assert.Contains("- [11] curseur « Musique » valeur actuelle 80 (0 à 100)", text);
        Assert.Contains("- [12:1] porte de niveau « Niveau 2 (verrouillé) » — désactivé", text);
    }

    [Fact]
    public void EventsUseCompactCoordinates()
    {
        var game = NewStoreroom();
        var result = game.Move(new Position(1, 2));

        Assert.Contains("Le magus se déplace de (1,1) à (1,2).", EventText.Describe(result.Events, game));
    }
}
