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
    private const float LanguageChoiceWidth = 120f;
    private bool _pendingConclusion;
    private readonly HashSet<Discovery> _discovered = new();
    private readonly Queue<Discovery> _discoveryQueue = new();
    private DiscoveryPopup? _discoveryPopup;

    public override void _Ready()
    {
        RenderingServer.SetDefaultClearColor(LevelView.BackgroundColor);
        UserSettings.Load();

        _view = new LevelView { Name = "LevelView" };
        AddChild(_view);
        _view.AnimationsFinished += OnAnimationsFinished;

        _camera = new Camera2D { Name = "Camera", PositionSmoothingEnabled = true };
        _view.AddChild(_camera);

        _hud = new Hud { Name = "Hud", Visible = false };
        AddChild(_hud);
        _hud.PausePressed += ShowPause;
        _hud.HelpPressed += ShowHelp;

        try
        {
            Texts.Load(UserSettings.Language);
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
        if (_game is null || _screen is not null || _discoveryPopup is not null || _game.Status != GameStatus.Playing || _view.IsBusy)
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
        ShowScreen(MenuScreen.CreateIllustrated(
            Texts.StoryTitle,
            Texts.Story,
            new StoryIllustration(),
            (Texts.NextButton, () => ShowSpacioMagus(() => StartLevel(0))),
            (Texts.SkipButton, () => StartLevel(0))));
    }

    private void ShowSpacioMagus(Action done)
    {
        ShowScreen(MenuScreen.CreateIllustrated(
            Texts.SpacioMagusTitle,
            Texts.SpacioMagus,
            new SpacioMagusIllustration(),
            (Texts.NextButton, () => ShowSpacioCreatura(done)),
            (Texts.SkipButton, done)));
    }

    private void ShowSpacioCreatura(Action done)
    {
        ShowScreen(MenuScreen.CreateIllustrated(
            Texts.SpacioCreaturaTitle,
            Texts.SpacioCreatura,
            new SpacioCreaturaIllustration(),
            (Texts.NextButton, done)));
    }

    private void ShowVictory()
    {
        var buttons = new List<(string Text, Action Action)>();
        if (_levelIndex + 1 < _levels.Count)
        {
            var next = _levelIndex + 1;
            buttons.Add((Texts.NextLevelButton, () => StartLevel(next)));
        }

        buttons.Add((Texts.PlayAgainButton, () => StartLevel(0)));
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
            (Texts.PlayAgainButton, () => StartLevel(index))));
    }

    private void ShowPause()
    {
        if (_game is null || _screen is not null || _discoveryPopup is not null)
        {
            return;
        }

        var index = _levelIndex;
        ShowScreen(MenuScreen.CreateWithControl(
            Texts.PauseTitle,
            string.Empty,
            LanguageChoice(),
            (Texts.ResumeButton, () => Resume()),
            (Texts.RestartButton, () => StartLevel(index)),
            (UserSettings.TipsEnabled ? Texts.TipsOn : Texts.TipsOff, () => ToggleTips()),
            (Texts.CreditsButton, () => ShowCredits())));
    }

    private Control LanguageChoice()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var label = new Label { Text = Texts.LanguageLabel, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeColorOverride("font_color", GuiAssets.TextOnParchment);
        row.AddChild(label);

        var choice = new OptionButton { CustomMinimumSize = new Vector2(LanguageChoiceWidth, 28) };
        for (var index = 0; index < Texts.Languages.Count; index++)
        {
            choice.AddItem(Texts.LanguageName(Texts.Languages[index]), index);
            if (Texts.Languages[index] == Texts.Language)
            {
                choice.Selected = index;
            }
        }

        choice.ItemSelected += selected => ChangeLanguage(Texts.Languages[(int)selected]);
        row.AddChild(choice);
        return row;
    }

    private void ChangeLanguage(string language)
    {
        if (language == Texts.Language)
        {
            return;
        }

        try
        {
            Texts.Load(language);
        }
        catch (System.IO.IOException exception)
        {
            GD.PushError(exception.Message);
            return;
        }

        UserSettings.SetLanguage(language);
        CloseScreen();
        ShowPause();
    }

    private void ToggleTips()
    {
        UserSettings.SetTipsEnabled(!UserSettings.TipsEnabled);
        CloseScreen();
        ShowPause();
    }

    private void ShowHelp()
    {
        if (_game is null || _screen is not null || _discoveryPopup is not null)
        {
            return;
        }

        ShowSpacioMagus(Resume);
    }

    private void ShowCredits()
    {
        ShowScreen(MenuScreen.Create(Texts.CreditsTitle, Texts.Credits, (Texts.ResumeButton, () => Resume())));
    }

    private void Resume()
    {
        CloseScreen();
        _hud.Visible = _game is not null;
    }

    private void ShowError(string message)
    {
        GD.PushError(message);
        ShowScreen(MenuScreen.Create(Texts.ErrorTitle, message));
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
        _pendingConclusion = false;
        _hud.ShowMessage(Texts.LevelStart(loaded.Definition.Name));
        Refresh();
        _camera.ResetSmoothing();
        CheckDiscoveries();
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

        Reject(cell);
    }

    private void TapFirstCreature(Game game, Cell cell)
    {
        var candidates = game.SwapCandidates().Select(monster => monster.Position).ToList();
        if (!candidates.Contains(cell) || game.Magus.Magic < game.Rules.SpellCost || candidates.Count < 2)
        {
            Reject(cell);
            return;
        }

        _firstCreature = cell;
        _view.Flash(cell, TapKind.Creature);
        Refresh();
    }

    private void TapSecondCreature(Game game, Cell first, Cell cell)
    {
        _firstCreature = null;
        if (cell == first)
        {
            _view.Flash(cell, TapKind.Creature);
            Refresh();
            return;
        }

        var candidates = game.SwapCandidates().Select(monster => monster.Position).ToList();
        if (!candidates.Contains(cell))
        {
            _view.Flash(cell, TapKind.Invalid);
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

    private void Reject(Cell cell)
    {
        _view.Flash(cell, TapKind.Invalid);
    }

    private void Apply(ActionResult result, Cell cell, TapKind kind)
    {
        if (_game is null)
        {
            return;
        }

        if (!result.Success)
        {
            Reject(cell);
            Refresh();
            return;
        }

        _view.Flash(cell, kind);
        _firstCreature = null;
        _view.PlayEvents(result.Events);
        if (_view.IsBusy)
        {
            _pendingConclusion = true;
            return;
        }

        Conclude();
    }

    public override void _Process(double delta)
    {
        if (_game is not null)
        {
            _camera.Position = _view.MagusFocus;
        }
    }

    private void OnAnimationsFinished()
    {
        if (!_pendingConclusion)
        {
            return;
        }

        _pendingConclusion = false;
        Conclude();
    }

    private void Conclude()
    {
        if (_game is null)
        {
            return;
        }

        Refresh();

        if (_game.Status == GameStatus.Won)
        {
            ShowVictory();
        }
        else if (_game.Status == GameStatus.Lost)
        {
            ShowDefeat();
        }
        else
        {
            CheckDiscoveries();
        }
    }

    private void CheckDiscoveries()
    {
        if (_game is null)
        {
            return;
        }

        var magus = _game.Magus.Position;
        bool Seen(Cell cell) => _game.HasLineOfSight(magus, cell);
        if (_game.Grilles.Any(grille => Seen(grille.Position)))
        {
            QueueDiscovery(Discovery.Door);
        }

        if (_game.Potions.Any(Seen))
        {
            QueueDiscovery(Discovery.Potion);
        }

        if (_game.Monsters.Any(monster => monster.Kind == MonsterKind.BlobVert && Seen(monster.Position)))
        {
            QueueDiscovery(Discovery.GreenBlob);
        }

        ShowNextDiscovery();
    }

    private void QueueDiscovery(Discovery discovery)
    {
        if (UserSettings.TipsEnabled && _discovered.Add(discovery))
        {
            _discoveryQueue.Enqueue(discovery);
        }
    }

    private void ShowNextDiscovery()
    {
        if (_discoveryPopup is not null || _discoveryQueue.Count == 0)
        {
            return;
        }

        _discoveryPopup = DiscoveryPopup.Create(_discoveryQueue.Dequeue());
        _discoveryPopup.Closed += OnDiscoveryClosed;
        AddChild(_discoveryPopup);
    }

    private void OnDiscoveryClosed(bool hideTips)
    {
        _discoveryPopup = null;
        if (hideTips)
        {
            UserSettings.SetTipsEnabled(false);
            _discoveryQueue.Clear();
            return;
        }

        ShowNextDiscovery();
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
        _camera.Position = _view.MagusFocus;

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
