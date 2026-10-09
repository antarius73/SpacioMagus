using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using SpacioMagus.Core;
using SpacioMagus.Core.Loading;
using Cell = SpacioMagus.Core.Position;

namespace SpacioMagus;

public partial class Main : Node
{
    private GameSettings _settings = GameSettings.Default;
    private Legend? _legend;
    private IReadOnlyList<string> _levels = Array.Empty<string>();
    private int _levelIndex;
    private Game? _game;
    private LevelView _view = null!;
    private Camera2D _camera = null!;
    private Hud _hud = null!;
    private MenuScreen? _screen;
    private Cell? _firstCreature;

    public override void _Ready()
    {
        RenderingServer.SetDefaultClearColor(LevelView.BackgroundColor);

        _view = new LevelView { Name = "LevelView" };
        AddChild(_view);

        _camera = new Camera2D { Name = "Camera", PositionSmoothingEnabled = true };
        _view.AddChild(_camera);

        _hud = new Hud { Name = "Hud", Visible = false };
        AddChild(_hud);
        _hud.PausePressed += ShowPause;
        _hud.HelpPressed += ShowHelp;

        try
        {
            _settings = GameData.LoadSettings();
            _legend = GameData.LoadLegend();
            _levels = GameData.LoadLevelList();
        }
        catch (Exception exception) when (exception is LevelLoadException or System.IO.IOException)
        {
            ShowError(exception.Message);
            return;
        }

        ShowStory();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_game is null || _screen is not null || _game.Status != GameStatus.Playing)
        {
            return;
        }

        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
        {
            return;
        }

        var point = _view.GetCanvasTransform().AffineInverse() * click.Position;
        HandleTap(_game, LevelView.CellAt(point));
        GetViewport().SetInputAsHandled();
    }

    private void ShowStory()
    {
        ShowScreen(MenuScreen.Create(
            Texts.StoryTitle,
            Texts.Story,
            ("Suivant", () => ShowPowers()),
            ("Passer", () => StartLevel(0))));
    }

    private void ShowPowers()
    {
        ShowScreen(MenuScreen.Create(
            Texts.PowersTitle,
            Texts.Powers,
            ("Suivant", () => StartLevel(0)),
            ("Passer", () => StartLevel(0))));
    }

    private void ShowVictory()
    {
        var buttons = new List<(string Text, Action Action)>();
        if (_levelIndex + 1 < _levels.Count)
        {
            var next = _levelIndex + 1;
            buttons.Add(("Niveau suivant", () => StartLevel(next)));
        }

        buttons.Add(("Rejouer", () => StartLevel(0)));
        var stars = new[] { GuiAssets.Star, GuiAssets.Star, GuiAssets.Star };
        ShowScreen(MenuScreen.Create(Texts.VictoryTitle, Texts.Victory, stars, buttons.ToArray()));
    }

    private void ShowDefeat()
    {
        var index = _levelIndex;
        ShowScreen(MenuScreen.Create(
            Texts.DefeatTitle,
            Texts.Defeat,
            new[] { GuiAssets.Cross },
            ("Rejouer", () => StartLevel(index))));
    }

    private void ShowPause()
    {
        if (_game is null || _screen is not null)
        {
            return;
        }

        var index = _levelIndex;
        ShowScreen(MenuScreen.Create(
            Texts.PauseTitle,
            string.Empty,
            ("Reprendre", () => Resume()),
            ("Recommencer", () => StartLevel(index)),
            ("Crédits", () => ShowCredits())));
    }

    private void ShowHelp()
    {
        if (_game is null || _screen is not null)
        {
            return;
        }

        ShowScreen(MenuScreen.Create(Texts.PowersTitle, Texts.Powers, ("Reprendre", () => Resume())));
    }

    private void ShowCredits()
    {
        ShowScreen(MenuScreen.Create(Texts.CreditsTitle, Texts.Credits, ("Reprendre", () => Resume())));
    }

    private void Resume()
    {
        CloseScreen();
        _hud.Visible = _game is not null;
    }

    private void ShowError(string message)
    {
        GD.PushError(message);
        ShowScreen(MenuScreen.Create("Erreur de données", message));
    }

    private void ShowScreen(MenuScreen screen)
    {
        CloseScreen();
        _hud.Visible = false;
        _screen = screen;
        AddChild(screen);
    }

    private void CloseScreen()
    {
        _screen?.QueueFree();
        _screen = null;
    }

    private void StartLevel(int index)
    {
        if (_legend is null || index < 0 || index >= _levels.Count)
        {
            return;
        }

        LoadedLevel loaded;
        try
        {
            loaded = GameData.LoadLevel(_levels[index], _legend, _settings.Rules);
        }
        catch (Exception exception) when (exception is LevelLoadException or System.IO.IOException)
        {
            ShowError(exception.Message);
            return;
        }

        _levelIndex = index;
        _game = new Game(loaded.Definition, loaded.Rules, new SystemRandomSource());
        _view.ShowLevel(_game, loaded.Decor);

        _camera.LimitLeft = 0;
        _camera.LimitRight = loaded.Definition.Width * LevelView.Tile;
        _camera.LimitTop = -(int)Hud.TopHeight;
        _camera.LimitBottom = loaded.Definition.Height * LevelView.Tile + (int)Hud.BottomHeight;

        CloseScreen();
        _hud.Visible = true;
        _firstCreature = null;
        _hud.ShowMessage(Texts.LevelStart(loaded.Definition.Name));
        Refresh();
        _camera.ResetSmoothing();
    }

    private void HandleTap(Game game, Cell cell)
    {
        if (_firstCreature is { } first)
        {
            TapSecondCreature(game, first, cell);
            return;
        }

        if (cell == game.Magus.Position)
        {
            Apply(game.Wait(), cell, TapKind.Rest);
            return;
        }

        if (game.MonsterAt(cell) is not null)
        {
            TapFirstCreature(game, cell);
            return;
        }

        if (game.Magus.Position.IsAdjacentTo(cell))
        {
            var grille = game.GrilleAt(cell);
            var result = grille is not null && !grille.IsOpen ? game.OpenGrille(cell) : game.Move(cell);
            Apply(result, cell, TapKind.Move);
            return;
        }

        if (game.TeleportTargets().Contains(cell))
        {
            Apply(game.CastSpacioMagus(cell), cell, TapKind.Teleport);
            return;
        }

        Reject(cell, OutOfZoneError(game, cell));
    }

    private void TapFirstCreature(Game game, Cell cell)
    {
        var candidates = game.SwapCandidates().Select(monster => monster.Position).ToList();
        if (!candidates.Contains(cell))
        {
            Reject(cell, OutOfZoneError(game, cell));
            return;
        }

        if (game.Magus.Magic < game.Rules.SpellCost)
        {
            Reject(cell, ActionError.NotEnoughMagic);
            return;
        }

        if (candidates.Count < 2)
        {
            Reject(cell, ActionError.NotEnoughCreatures);
            return;
        }

        _firstCreature = cell;
        _view.Flash(cell, TapKind.Creature);
        _hud.ShowMessage(Texts.SwapSecondHint);
        Refresh();
    }

    private void TapSecondCreature(Game game, Cell first, Cell cell)
    {
        _firstCreature = null;
        if (cell == first)
        {
            _view.Flash(cell, TapKind.Creature);
            _hud.ShowMessage(Texts.SwapCancelled);
            Refresh();
            return;
        }

        var candidates = game.SwapCandidates().Select(monster => monster.Position).ToList();
        if (!candidates.Contains(cell))
        {
            _view.Flash(cell, TapKind.Invalid);
            _hud.ShowMessage(Texts.SwapCancelled);
            Refresh();
            return;
        }

        var result = game.CastSpacioCreatura(first, cell);
        if (result.Success)
        {
            _view.Flash(first, TapKind.Creature);
        }

        Apply(result, cell, TapKind.Creature);
    }

    private static ActionError OutOfZoneError(Game game, Cell cell)
    {
        if (game.Magus.Position.ChebyshevTo(cell) > game.Rules.SpellRange)
        {
            return ActionError.OutOfRange;
        }

        return game.IsWalkable(cell) ? ActionError.NotVisible : ActionError.Blocked;
    }

    private void Reject(Cell cell, ActionError error)
    {
        _view.Flash(cell, TapKind.Invalid);
        _hud.ShowMessage(Texts.ForError(error));
    }

    private void Apply(ActionResult result, Cell cell, TapKind kind)
    {
        if (_game is null)
        {
            return;
        }

        if (!result.Success)
        {
            Reject(cell, result.Error);
            Refresh();
            return;
        }

        _view.Flash(cell, kind);
        _firstCreature = null;
        _view.PlayEvents(result.Events);
        _hud.ShowMessage(Texts.ForEvents(result.Events));
        Refresh();

        if (_game.Status == GameStatus.Won)
        {
            ShowVictory();
        }
        else if (_game.Status == GameStatus.Lost)
        {
            ShowDefeat();
        }
    }

    private void Refresh()
    {
        if (_game is null)
        {
            return;
        }

        var magus = _game.Magus;
        var canCast = magus.Magic >= _game.Rules.SpellCost;
        _hud.Refresh(magus.Magic, _game.Rules.MagicMax);
        _camera.Position = LevelView.CellCenter(magus.Position);

        var candidates = _game.SwapCandidates().Select(monster => monster.Position).ToList();
        var zone = _game.TeleportTargets().Concat(candidates).Append(magus.Position);
        _view.SetMagicZone(zone, canCast);

        if (_firstCreature is { } first)
        {
            _view.SetHighlights(candidates.Where(cell => cell != first), first);
        }
        else
        {
            _view.ClearHighlights();
        }
    }
}
