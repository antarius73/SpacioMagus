using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using SpacioMagus.Core;
using SpacioMagus.Core.Loading;
using SpacioMagus.Core.Saving;
using Cell = SpacioMagus.Core.Position;

namespace SpacioMagus;

public partial class Main : Node
{
    private GameSettings _settings = GameSettings.Default;
    private Legend? _legend;
    private IReadOnlyList<string> _levels = Array.Empty<string>();
    private int _levelIndex;
    private Game? _game;
    private GameJournal? _journal;
    private string _levelFingerprint = string.Empty;
    private string _settingsText = string.Empty;
    private string _legendText = string.Empty;
    private LevelView _view = null!;
    private Camera2D _camera = null!;
    private Hud _hud = null!;
    private CanvasLayer? _screen;
    private Cell? _firstCreature;
    private const float LanguageChoiceWidth = 120f;
    private bool _pendingConclusion;
    private const float PressSlop = 10f;
    private InfoPopup? _infoPopup;
    private InfoSubject? _infoSubject;
    private Func<CanvasLayer>? _screenBuilder;
    private (Cell Cell, Vector2 Screen)? _press;

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

        try
        {
            Texts.Load(UserSettings.Language);
            _settingsText = GameData.ReadText("regles.txt");
            _legendText = GameData.ReadText("legende.txt");
            _settings = GameData.LoadSettings();
            _legend = GameData.LoadLegend();
            _levels = GameData.LoadLevelList();
        }
        catch (Exception exception) when (exception is LevelLoadException or System.IO.IOException)
        {
            ShowError(exception.Message);
            return;
        }

        ShowTitle();
    }

    private SaveGame? ResumableSave() =>
        SaveStore.Load() is { } save && _levels.Contains(save.LevelFile) ? save : null;

    private void ShowTitle()
    {
        _game = null;
        _journal = null;
        _firstCreature = null;
        _pendingConclusion = false;
        CancelPress();
        _view.Visible = false;
        var canContinue = ResumableSave() is not null;
        ShowScreen(() => TitleScreen.Create(
            canContinue,
            RequestNewGame,
            ContinueGame,
            () => ShowSettings(ShowTitle),
            () => ShowCredits(ShowTitle)));
    }

    private void RequestNewGame()
    {
        if (ResumableSave() is null)
        {
            StartNewGame();
            return;
        }

        ShowScreen(() => MenuScreen.Create(
            Texts.ConfirmNewGameTitle,
            Texts.ConfirmNewGame,
            (Texts.ConfirmButton, StartNewGame),
            (Texts.CancelButton, ShowTitle)));
    }

    private void StartNewGame()
    {
        SaveStore.Delete();
        ShowStory();
    }

    private void ContinueGame()
    {
        if (ResumableSave() is not { } save || !TryResume(save))
        {
            ShowTitle();
        }
    }

    private bool TryResume(SaveGame save)
    {
        var index = _levels.ToList().IndexOf(save.LevelFile);
        if (index < 0)
        {
            GD.PushWarning($"Sauvegarde ignorée : le niveau « {save.LevelFile} » n'existe plus.");
            return false;
        }

        return StartLevel(index, save);
    }

    private static ulong NewSeed() => (ulong)Random.Shared.NextInt64();

    private void SaveProgress()
    {
        if (_journal is null || _levelIndex < 0 || _levelIndex >= _levels.Count)
        {
            return;
        }

        var levelFile = _levels[_levelIndex];
        switch (_journal.Game.Status)
        {
            case GameStatus.Playing:
                SaveStore.Write(SaveGame.InProgress(levelFile, _levelFingerprint, _journal));
                break;
            case GameStatus.Won when _levelIndex + 1 < _levels.Count:
                SaveStore.Write(SaveGame.LevelStart(_levels[_levelIndex + 1], NewSeed()));
                break;
            case GameStatus.Won:
                SaveStore.Delete();
                break;
            case GameStatus.Lost:
                SaveStore.Write(SaveGame.LevelStart(levelFile, NewSeed()));
                break;
        }
    }

    private ActionResult Play(GameAction action) =>
        _journal?.Play(action) ?? throw new InvalidOperationException("Aucune partie en cours.");

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_game is null || _screen is not null || _infoPopup is not null || _game.Status != GameStatus.Playing || _view.IsBusy)
        {
            CancelPress();
            return;
        }

        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } right:
                CancelPress();
                ShowInfo(CellAt(right.Position));
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press:
                _press = (CellAt(press.Position), press.Position);
                _view.BeginHold(_press.Value.Cell);
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }:
                if (_press is { } released)
                {
                    CancelPress();
                    HandleTap(_game, released.Cell);
                }

                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseMotion motion when _press is { } moving:
                if (motion.Position.DistanceTo(moving.Screen) > PressSlop)
                {
                    CancelPress();
                }

                break;
        }
    }

    private Cell CellAt(Vector2 screen) => LevelView.CellAt(_view.GetCanvasTransform().AffineInverse() * screen);

    private void CancelPress()
    {
        _press = null;
        _view.EndHold();
    }

    private void ShowInfo(Cell cell)
    {
        if (_game is null)
        {
            return;
        }

        if (cell == _game.Magus.Position)
        {
            ShowSpacioMagus(Resume);
            return;
        }

        InfoSubject? subject = _game.MonsterAt(cell) is { Kind: MonsterKind.BlobVert } ? InfoSubject.GreenBlob
            : _game.MonsterAt(cell) is { Kind: MonsterKind.BlobRouge } ? InfoSubject.RedBlob
            : _game.GrilleAt(cell) is not null ? InfoSubject.Door
            : _game.Level.Torches.Contains(cell) ? InfoSubject.Torch
            : _game.Level.TerrainAt(cell) == Terrain.Brazier ? InfoSubject.Brazier
            : _game.Potions.Contains(cell) ? InfoSubject.Potion
            : _game.Level.TerrainAt(cell) == Terrain.Stairs ? InfoSubject.Stairs
            : (InfoSubject?)null;
        if (subject is not { } value)
        {
            return;
        }

        OpenInfoPopup(value);
    }

    private void OpenInfoPopup(InfoSubject subject)
    {
        var popup = InfoPopup.Create(subject);
        popup.Closed += () =>
        {
            if (_infoPopup == popup)
            {
                _infoPopup = null;
                _infoSubject = null;
            }
        };
        _infoPopup = popup;
        _infoSubject = subject;
        AddChild(popup);
    }

    public override void _Input(InputEvent @event)
    {
        if (OS.IsDebugBuild() && @event is InputEventKey { Keycode: Key.F5, Pressed: true, Echo: false })
        {
            ReloadTexts();
            GetViewport().SetInputAsHandled();
        }
    }

    private void ReloadTexts()
    {
        try
        {
            Texts.Load(Texts.Language);
        }
        catch (Exception exception) when (exception is LevelLoadException or System.IO.IOException)
        {
            GD.PushError(exception.Message);
            return;
        }

        GD.Print($"Textes rechargés ({Texts.Language}).");
        if (_screenBuilder is { } build)
        {
            ShowScreen(build);
        }

        if (_infoPopup is { } popup && _infoSubject is { } subject)
        {
            _infoPopup = null;
            popup.QueueFree();
            OpenInfoPopup(subject);
        }
    }

    private void ShowStory()
    {
        ShowScreen(() => MenuScreen.CreateIllustrated(
            Texts.StoryTitle,
            Texts.Story,
            new StoryIllustration(),
            (Texts.NextButton, () => ShowSpacioMagus(() => ShowGesture(() => StartLevel(0)))),
            (Texts.SkipButton, () => StartLevel(0))));
    }

    private void ShowSpacioMagus(Action done)
    {
        ShowScreen(() => MenuScreen.CreateIllustrated(
            Texts.SpacioMagusTitle,
            Texts.SpacioMagus,
            new SpacioMagusIllustration(),
            (Texts.NextButton, () => ShowSpacioCreatura(done)),
            (Texts.SkipButton, done)));
    }

    private void ShowGesture(Action done)
    {
        ShowScreen(() => MenuScreen.CreateIllustrated(
            Texts.GestureTitle,
            Texts.Gesture,
            new GestureIllustration(),
            (Texts.NextButton, done)));
    }

    private void ShowSpacioCreatura(Action done)
    {
        ShowScreen(() => MenuScreen.CreateIllustrated(
            Texts.SpacioCreaturaTitle,
            Texts.SpacioCreatura,
            new SpacioCreaturaIllustration(),
            (Texts.NextButton, done)));
    }

    private void ShowVictory()
    {
        var next = _levelIndex + 1;
        ShowScreen(() =>
        {
            var buttons = new List<(string Text, Action Action)>();
            if (next < _levels.Count)
            {
                buttons.Add((Texts.NextLevelButton, () => StartLevel(next)));
            }

            buttons.Add((Texts.PlayAgainButton, () => StartLevel(0)));
            buttons.Add((Texts.MainMenuButton, ShowTitle));
            var stars = new[] { GuiAssets.Star, GuiAssets.Star, GuiAssets.Star };
            return MenuScreen.Create(Texts.VictoryTitle, Texts.Victory, stars, buttons.ToArray());
        });
    }

    private void ShowDefeat()
    {
        var index = _levelIndex;
        ShowScreen(() => MenuScreen.Create(
            Texts.DefeatTitle,
            Texts.Defeat,
            new[] { GuiAssets.Cross },
            (Texts.PlayAgainButton, () => StartLevel(index)),
            (Texts.MainMenuButton, ShowTitle)));
    }

    private void ShowPause()
    {
        if (_game is null || _screen is not null || _infoPopup is not null)
        {
            return;
        }

        ShowPauseMenu();
    }

    private void ShowPauseMenu()
    {
        var index = _levelIndex;
        ShowScreen(() => MenuScreen.Create(
            Texts.PauseTitle,
            string.Empty,
            (Texts.ResumeButton, () => Resume()),
            (Texts.RestartButton, () => StartLevel(index)),
            (Texts.SettingsButton, () => ShowSettings(ShowPauseMenu)),
            (Texts.CreditsButton, () => ShowCredits(ShowPauseMenu)),
            (Texts.MainMenuButton, ShowTitle)));
    }

    private void ShowSettings(Action back)
    {
        ShowScreen(() => MenuScreen.CreateWithControl(
            Texts.SettingsTitle,
            string.Empty,
            LanguageChoice(back),
            (Texts.BackButton, back)));
    }

    private Control LanguageChoice(Action back)
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

        choice.ItemSelected += selected => ChangeLanguage(Texts.Languages[(int)selected], back);
        row.AddChild(choice);
        return row;
    }

    private void ChangeLanguage(string language, Action back)
    {
        if (language == Texts.Language)
        {
            return;
        }

        try
        {
            Texts.Load(language);
        }
        catch (Exception exception) when (exception is LevelLoadException or System.IO.IOException)
        {
            GD.PushError(exception.Message);
            return;
        }

        UserSettings.SetLanguage(language);
        ShowSettings(back);
    }

    private void ShowCredits(Action back)
    {
        ShowScreen(() => MenuScreen.Create(Texts.CreditsTitle, Texts.Credits, (Texts.BackButton, back)));
    }

    private void Resume()
    {
        CloseScreen();
        _hud.Visible = _game is not null;
    }

    private void ShowError(string message)
    {
        GD.PushError(message);
        ShowScreen(() => MenuScreen.Create(Texts.ErrorTitle, message));
    }

    private void ShowScreen(Func<CanvasLayer> build)
    {
        CloseScreen();
        _hud.Visible = false;
        _screenBuilder = build;
        _screen = build();
        AddChild(_screen);
    }

    private void CloseScreen()
    {
        _screen?.QueueFree();
        _screen = null;
        _screenBuilder = null;
    }

    private bool StartLevel(int index, SaveGame? save = null)
    {
        if (_legend is null || index < 0 || index >= _levels.Count)
        {
            return false;
        }

        var levelFile = _levels[index];
        string content;
        LoadedLevel loaded;
        try
        {
            content = GameData.ReadLevel(levelFile);
            loaded = GameData.LoadLevel(levelFile, content, _legend, _settings.Rules);
        }
        catch (Exception exception) when (exception is LevelLoadException or System.IO.IOException)
        {
            ShowError(exception.Message);
            return false;
        }

        var fingerprint = GameDigest.Fingerprint(_settingsText, _legendText, content);
        GameJournal journal;
        if (save is not null)
        {
            var restored = SaveRestorer.Restore(save, loaded.Definition, loaded.Rules, fingerprint);
            if (restored.FellBack)
            {
                GD.PushWarning($"Sauvegarde incompatible avec « {levelFile} » : reprise au début du niveau.");
            }

            journal = restored.Journal;
        }
        else
        {
            journal = new GameJournal(loaded.Definition, loaded.Rules, NewSeed());
        }

        _levelIndex = index;
        _view.Visible = true;
        _journal = journal;
        _levelFingerprint = fingerprint;
        _game = journal.Game;
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
        SaveProgress();
        Refresh();
        _camera.ResetSmoothing();
        return true;
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
            Apply(Play(new WaitAction()), cell, TapKind.Wait);
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
            var result = grille is not null && !grille.IsOpen ? Play(new OpenGrilleAction(cell)) : Play(new MoveAction(cell));
            Apply(result, cell, TapKind.Move);
            return;
        }

        if (game.TeleportTargets().Contains(cell))
        {
            Apply(Play(new SpacioMagusAction(cell)), cell, TapKind.Teleport);
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

        var result = Play(new SpacioCreaturaAction(first, cell));
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
        SaveProgress();
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
        if (_game is null)
        {
            return;
        }

        _camera.Position = _view.MagusFocus;
        if (_press is { } held && _view.HoldElapsed >= LevelView.LongPressDuration)
        {
            CancelPress();
            ShowInfo(held.Cell);
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
