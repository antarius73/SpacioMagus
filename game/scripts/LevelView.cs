using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using SpacioMagus.Core;
using SpacioMagus.Core.Loading;
using Cell = SpacioMagus.Core.Position;

namespace SpacioMagus;

public enum TapKind
{
    Move,
    Teleport,
    Creature,
    Wait,
    Invalid,
}

public partial class LevelView : Node2D
{
    public const int Tile = 32;
    public static readonly Color BackgroundColor = Color.Color8(29, 28, 43);

    private const int SubTile = 16;
    private const int TilesetColumns = 17;
    private const int FloorTile = 138;
    private const int FaceBottomTile = 86;
    private const int FaceTopTile = 69;
    private const int LedgeTile = 52;
    private const int ThinFaceBottomTile = 341;
    private const int LedgeCut = 3;
    private const int FaceBesideTopEastTile = 39;
    private const int FaceBesideBottomEastTile = 56;
    private const int FaceBesideTopWestTile = 41;
    private const int FaceBesideBottomWestTile = 58;
    private const int InnerCornerNorthWestTile = 75;
    private const int InnerCornerNorthEastTile = 73;
    private const int FaceCornerSouthEastTile = 22;
    private const int FaceCornerSouthWestTile = 24;
    private const int TopNorthWestTile = 18;
    private const int TopNorthTile = 19;
    private const int TopNorthEastTile = 20;
    private const int TopWestTile = 35;
    private const int TopEastTile = 37;
    private static readonly int[] CrackTiles = { 228, 229, 245, 246 };
    private const int ShadedCrackOffset = 2;
    private const int ShadeCenterTile = 250;
    private const int ShadeNorthWestTile = 232;
    private const int ShadeNorthTile = 233;
    private const int ShadeNorthEastTile = 234;
    private const int ShadeWestTile = 249;
    private const int ShadeEastTile = 251;
    private const int ShadeSouthWestTile = 266;
    private const int ShadeSouthTile = 267;
    private const int ShadeSouthEastTile = 268;
    private const int ShadeInnerSouthEastTile = 235;
    private const int ShadeInnerSouthWestTile = 236;
    private const int ShadeInnerNorthEastTile = 252;
    private const int ShadeInnerNorthWestTile = 253;

    private static readonly Rect2 StairsRegion = new(16, 80, 32, 32);
    private static readonly Rect2 ShieldRegion = new(96, 112, 16, 16);
    private static readonly Dictionary<string, Rect2> HangingRegions = new()
    {
        [ElementIds.RedHanging] = new Rect2(0, 0, 32, 32),
        [ElementIds.RedBoneHanging] = new Rect2(32, 0, 32, 32),
        [ElementIds.RedTornHanging] = new Rect2(64, 0, 32, 32),
        [ElementIds.GreenHanging] = new Rect2(0, 32, 32, 32),
        [ElementIds.GreenBoneHanging] = new Rect2(32, 32, 32, 32),
    };
    private const int BannerOffsetY = -2;
    private static readonly Rect2[] CrateRegions =
    {
        new(56, 82, 16, 30), new(80, 82, 16, 20), new(96, 80, 16, 24), new(160, 82, 16, 20), new(176, 80, 16, 24),
    };
    private static readonly Rect2[] BarrelRegions = { new(112, 80, 16, 24), new(192, 80, 16, 24) };
    private const int ContainerBottomMargin = 2;
    private const double BreakBlinkDuration = 0.25;
    private const int BreakBlinks = 3;
    private const double PotionAppearDelay = 0.3;
    private const double PotionBounceDuration = 0.35;
    private const float PotionBounceHeight = 8f;

    private const double CrackChance = 0.4;
    private const double WebChance = 0.5;
    private const double GrilleAnimationDuration = 0.35;
    private const int GrilleFrames = 5;
    private const int DoorFrameStride = 48;
    private const int ThickDoorColumn = 32;
    private const int ThickDoorTop = 16;
    private const int ThinDoorColumn = 96;
    private const int WebSize = 16;
    private const int WebOffsetY = -2;
    private const int ShieldOffsetY = 5;
    private const int FireFrames = 6;
    private const int FireFrameHeight = 48;
    private const int FireColumn = 39;
    private const int FireOffsetY = -8;
    private static readonly Color CandidateColor = new(0.8f, 0.55f, 1f, 0.22f);
    private static readonly Color ZoneEdgeColor = new(0.55f, 0.85f, 1f);
    private static readonly Color ZoneEdgeEmptyColor = new(0.6f, 0.6f, 0.7f);
    private static readonly Color MoveFlashColor = new(1f, 1f, 1f);
    private static readonly Color TeleportFlashColor = new(0.45f, 0.85f, 1f);
    private static readonly Color CreatureFlashColor = new(0.85f, 0.6f, 1f);
    private static readonly Color WaitFlashColor = new(1f, 0.85f, 0.4f);
    private static readonly Color InvalidFlashColor = new(1f, 0.3f, 0.25f);
    private const float OutsideZoneDarkness = 0.5f;
    private const float ZoneFadeSpeed = 2.5f;
    private const double FlashDuration = 0.4;
    private const float FlashGrowth = 4f;
    private const int FlashCornerRadius = 6;
    private static readonly Color StunnedColor = new(0.6f, 0.6f, 0.75f);
    private static readonly Color FireFearColor = new(1f, 0.55f, 0.2f, 0.14f);
    private static readonly Color WaterFearColor = new(0.3f, 0.6f, 1f, 0.14f);
    private static readonly Color WaterColor = new(0.16f, 0.36f, 0.62f);
    private const int GreenBlobRow = 5;
    private const int RedBlobRow = 6;
    private const double MutationBlinkDuration = 0.18;
    private const int BrazierColumn = 128;
    private const int BrazierSize = 48;
    private const int BrazierOffsetX = -8;
    private const int BrazierOffsetY = -10;
    private const float DoorCounterOffsetY = 6f;

    private readonly HashSet<Cell> _highlights = new();
    private readonly HashSet<Cell> _zone = new();
    private readonly StyleBoxFlat _roundedBox = new() { AntiAliasing = false };
    private readonly List<(Cell Cell, Color Color, double Start)> _flashes = new();
    private readonly Dictionary<(int X, int Y), int> _shading = new();
    private float[] _darkness = Array.Empty<float>();
    private float[] _darknessTarget = Array.Empty<float>();
    private bool _canCast = true;
    private readonly List<(int TileId, Vector2 Position)> _terrainTiles = new();
    private readonly List<(int TileId, Vector2 Position)> _ledges = new();
    private readonly List<(int TileId, Vector2 Center, int Turns)> _cracks = new();
    private readonly List<(Vector2 Position, bool FlipX)> _webs = new();
    private readonly List<(Rect2 Source, Rect2 Target)> _shields = new();
    private readonly List<(Rect2 Source, Vector2 Position)> _banners = new();
    private readonly List<Cell> _torches = new();
    private readonly List<Cell> _stairs = new();
    private readonly List<Cell> _braziers = new();
    private readonly List<Cell> _water = new();
    private readonly Dictionary<Cell, Rect2> _containerLooks = new();
    private readonly Dictionary<Cell, (double Start, Rect2 Look)> _breaks = new();
    private readonly HashSet<Cell> _thinDoors = new();
    private readonly Dictionary<Cell, (bool Open, double ChangedAt)> _grilleStates = new();
    private Texture2D _tileset = null!;
    private Texture2D _doors = null!;
    private Texture2D _fire = null!;
    private Texture2D _objects = null!;
    private Texture2D _items = null!;
    private Texture2D _web = null!;
    private Texture2D _animations = null!;
    private Texture2D _potion = null!;
    private Texture2D _magusPixel = null!;
    private Game? _game;
    private IReadOnlyList<DecorItem> _decor = Array.Empty<DecorItem>();
    private Cell? _selected;
    private double _time;
    private int _magusDirection;
    private (Cell From, double Start)? _magusGlide;
    private (Cell From, Cell To, double Start)? _teleport;
    private (Cell First, Cell Second, double Start)? _swap;
    private readonly Dictionary<int, Cell> _swapOrigins = new();
    private readonly Dictionary<int, (int Previous, int Current, double Since)> _monsterFacing = new();
    private (Cell Cell, double Start)? _selectionShock;
    private (Cell Cell, double Start)? _fadingShock;
    private bool _wasBusy;
    private (Cell Cell, double Start)? _hold;

    public double HoldElapsed => _hold is { } hold ? _time - hold.Start : 0.0;

    public event Action? AnimationsFinished;

    public bool IsBusy =>
        (_teleport is { } teleport && _time - teleport.Start < SpellFx.TeleportDuration)
        || (_swap is { } swap && _time - swap.Start < SpellFx.SwapDuration);

    public Vector2 MagusFocus
    {
        get
        {
            if (_game is null)
            {
                return Vector2.Zero;
            }

            if (_teleport is { } teleport && _time - teleport.Start < SpellFx.TeleportHalfDuration)
            {
                return CellCenter(teleport.From);
            }

            return Shown(_game.Magus.Position, _magusGlide, out _) + new Vector2(Tile / 2f, Tile / 2f);
        }
    }
    private readonly Dictionary<int, (Cell From, double Start)> _monsterGlides = new();

    private const double MoveDuration = 0.15;
    private const int DefaultFacing = -1;
    private const float HoldRingRadius = 14f;
    private const float HoldRingWidth = 2f;
    private const int HoldRingPoints = 32;
    private static readonly Color HoldRingColor = new(0.95f, 0.89f, 0.74f, 0.9f);

    public const double LongPressDuration = 0.5;
    private const int WalkRowOffset = 0;
    private const int IdleRowOffset = 8;

    public static Vector2 CellCenter(Cell cell) => new(cell.X * Tile + Tile / 2f, cell.Y * Tile + Tile / 2f);

    public static Cell CellAt(Vector2 point) => new(Mathf.FloorToInt(point.X / Tile), Mathf.FloorToInt(point.Y / Tile));

    public override void _Ready()
    {
        _tileset = LoadTexture("res://assets/sprites/donjon_murs_sol.png");
        _doors = LoadTexture("res://assets/sprites/donjon_portes.png");
        _fire = LoadTexture("res://assets/sprites/donjon_feu.png");
        _objects = LoadTexture("res://assets/sprites/donjon_objets.png");
        _items = LoadTexture("res://assets/sprites/items.png");
        _web = LoadTexture("res://assets/sprites/toile.png");
        _animations = LoadTexture("res://assets/sprites/animations.png");
        _potion = LoadTexture("res://assets/sprites/fiole_bleue.png");
        _magusPixel = LoadTexture("res://assets/sprites/magus_pixel.png");
    }

    public void ShowLevel(Game game, IReadOnlyList<DecorItem> decor)
    {
        _game = game;
        _decor = decor;
        BuildTerrain(game.Level);
        GenerateDecor(game.Level, new Random());
        _grilleStates.Clear();
        _magusDirection = 0;
        _magusGlide = null;
        _teleport = null;
        _swap = null;
        _swapOrigins.Clear();
        _hold = null;
        _selectionShock = null;
        _fadingShock = null;
        _wasBusy = false;
        _monsterGlides.Clear();
        _monsterFacing.Clear();
        _flashes.Clear();
        _zone.Clear();
        _darkness = new float[game.Level.Width * game.Level.Height];
        _darknessTarget = new float[_darkness.Length];
        Array.Fill(_darkness, OutsideZoneDarkness);
        Array.Fill(_darknessTarget, OutsideZoneDarkness);
        ClearHighlights();
    }

    public void SetMagicZone(IEnumerable<Cell> zone, bool canCast)
    {
        _zone.Clear();
        foreach (var cell in zone)
        {
            _zone.Add(cell);
        }

        _canCast = canCast;
        if (_game is null)
        {
            return;
        }

        var level = _game.Level;
        for (var y = 0; y < level.Height; y++)
        {
            for (var x = 0; x < level.Width; x++)
            {
                var cell = new Cell(x, y);
                _darknessTarget[y * level.Width + x] = IsLit(level, cell) ? 0f : OutsideZoneDarkness;
            }
        }

        QueueRedraw();
    }

    public void BeginHold(Cell cell)
    {
        _hold = (cell, _time);
    }

    public void EndHold()
    {
        _hold = null;
    }

    public void Flash(Cell cell, TapKind kind)
    {
        var color = kind switch
        {
            TapKind.Move => MoveFlashColor,
            TapKind.Teleport => TeleportFlashColor,
            TapKind.Creature => CreatureFlashColor,
            TapKind.Wait => WaitFlashColor,
            _ => InvalidFlashColor,
        };
        _flashes.Add((cell, color, _time));
        QueueRedraw();
    }

    public void SetHighlights(IEnumerable<Cell> cells, Cell? selected)
    {
        _highlights.Clear();
        foreach (var cell in cells)
        {
            _highlights.Add(cell);
        }

        SetSelected(selected);
        QueueRedraw();
    }

    public void PlayEvents(IReadOnlyList<GameEvent> events)
    {
        var monstersStart = _time;
        foreach (var gameEvent in events)
        {
            if (gameEvent is ContainerBroken broken && _containerLooks.TryGetValue(broken.Position, out var look))
            {
                _breaks[broken.Position] = (_time, look);
                _containerLooks.Remove(broken.Position);
            }

            if (gameEvent is MagusMoved { Cause: MoveCause.Teleport } teleport)
            {
                _teleport = (teleport.From, teleport.To, _time);
                monstersStart = _time + SpellFx.TeleportDuration;
            }

            if (gameEvent is MonsterMoved { Cause: MoveCause.Swap } swapped)
            {
                _swapOrigins[swapped.MonsterId] = swapped.From;
                if (_swap is not { } current || current.Start != _time)
                {
                    _swap = (swapped.From, swapped.To, _time);
                }

                monstersStart = _time + SpellFx.SwapDuration;
                _selected = null;
                _selectionShock = null;
                _fadingShock = null;
            }
        }

        foreach (var gameEvent in events)
        {
            switch (gameEvent)
            {
                case MagusMoved moved when moved.Cause is MoveCause.Step or MoveCause.Pushed:
                    _magusGlide = (moved.From, _time);
                    if (moved.Cause == MoveCause.Step)
                    {
                        _magusDirection = DirectionIndex(moved.To - moved.From);
                    }

                    break;
                case MagusMoved:
                    _magusGlide = null;
                    break;
                case MonsterMoved moved when moved.Cause is MoveCause.Step or MoveCause.Pushed:
                    _monsterGlides[moved.MonsterId] = (moved.From, monstersStart);
                    break;
                case MonsterMoved moved:
                    _monsterGlides.Remove(moved.MonsterId);
                    break;
            }
        }

        UpdateFacings(events, monstersStart);
    }

    private void UpdateFacings(IReadOnlyList<GameEvent> events, double since)
    {
        if (_game is null)
        {
            return;
        }

        var swapped = new HashSet<int>();
        var steps = new Dictionary<int, int>();
        foreach (var gameEvent in events)
        {
            if (gameEvent is not MonsterMoved moved)
            {
                continue;
            }

            if (moved.Cause == MoveCause.Swap)
            {
                swapped.Add(moved.MonsterId);
            }
            else if (moved.Cause == MoveCause.Step)
            {
                steps[moved.MonsterId] = Math.Sign(moved.To.X - moved.From.X);
            }
        }

        var magus = _game.Magus.Position;
        foreach (var monster in _game.Monsters)
        {
            if (swapped.Contains(monster.Id))
            {
                continue;
            }

            var direction = 0;
            if (monster.IsTracking && !monster.IsStunned)
            {
                direction = Math.Sign(magus.X - monster.Position.X);
            }
            else if (steps.TryGetValue(monster.Id, out var step))
            {
                direction = step;
            }

            var current = Facing(monster.Id);
            if (direction != 0 && direction != current)
            {
                _monsterFacing[monster.Id] = (current, direction, since);
            }
        }
    }

    private int Facing(int monsterId)
    {
        if (!_monsterFacing.TryGetValue(monsterId, out var facing))
        {
            return DefaultFacing;
        }

        return _time >= facing.Since ? facing.Current : facing.Previous;
    }

    public void ClearHighlights()
    {
        _highlights.Clear();
        SetSelected(null);
        QueueRedraw();
    }

    private void SetSelected(Cell? selected)
    {
        if (selected == _selected)
        {
            return;
        }

        if (_selectionShock is { } previous)
        {
            _fadingShock = (previous.Cell, _time);
        }

        _selected = selected;
        _selectionShock = selected is { } cell ? (cell, _time) : null;
    }

    public override void _Process(double delta)
    {
        _time += delta;
        var busy = IsBusy;
        if (_wasBusy && !busy)
        {
            _teleport = null;
            _swap = null;
            _swapOrigins.Clear();
            AnimationsFinished?.Invoke();
        }

        _wasBusy = busy;
        var step = (float)delta * ZoneFadeSpeed * OutsideZoneDarkness;
        for (var index = 0; index < _darkness.Length; index++)
        {
            _darkness[index] = Mathf.MoveToward(_darkness[index], _darknessTarget[index], step);
        }

        _flashes.RemoveAll(flash => _time - flash.Start > FlashDuration);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_game is null)
        {
            return;
        }

        var frame = (int)(_time * 5) % 4;

        foreach (var (tileId, position) in _terrainTiles)
        {
            DrawSubTile(tileId, position);
        }

        foreach (var (tileId, center, turns) in _cracks)
        {
            DrawSetTransform(center, turns * Mathf.Pi / 2f, Vector2.One);
            DrawTextureRectRegion(_tileset, new Rect2(-SubTile / 2f, -SubTile / 2f, SubTile, SubTile), TileSource(tileId));
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }

        foreach (var stairs in _stairs)
        {
            DrawTextureRectRegion(_objects, CellRect(stairs), StairsRegion);
        }

        foreach (var (source, position) in _banners)
        {
            DrawTextureRectRegion(_items, new Rect2(position, source.Size), source);
        }

        foreach (var (source, target) in _shields)
        {
            DrawTextureRectRegion(_objects, target, source);
        }

        var fireSource = new Rect2(FireColumn, (int)(_time * 8) % FireFrames * FireFrameHeight, Tile, FireFrameHeight);
        foreach (var torch in _torches)
        {
            DrawFire(torch, fireSource);
        }

        foreach (var (position, flipX) in _webs)
        {
            DrawWeb(position, flipX);
        }

        foreach (var cell in _water)
        {
            DrawRect(CellRect(cell), WaterColor);
        }

        foreach (var cell in _game.FearZone(MonsterKind.BlobVert))
        {
            DrawRect(CellRect(cell), FireFearColor);
        }

        foreach (var cell in _game.FearZone(MonsterKind.BlobRouge))
        {
            DrawRect(CellRect(cell), WaterFearColor);
        }

        foreach (var potion in _game.Potions)
        {
            var offset = 0f;
            if (_breaks.TryGetValue(potion, out var revealed))
            {
                var since = _time - revealed.Start - PotionAppearDelay;
                if (since < 0)
                {
                    continue;
                }

                if (since < PotionBounceDuration)
                {
                    var progress = (float)(since / PotionBounceDuration);
                    offset = -Mathf.Round(Mathf.Abs(Mathf.Cos(progress * Mathf.Pi * 2.5f)) * PotionBounceHeight * (1f - progress));
                }
            }

            var target = CellRect(potion);
            target.Position += new Vector2(0, offset);
            DrawTextureRectRegion(_potion, target, new Rect2(frame * 32, 0, 32, 32));
        }

        foreach (var container in _game.Containers)
        {
            if (_containerLooks.TryGetValue(container, out var look))
            {
                DrawContainer(container, look, Colors.White);
            }
        }

        foreach (var (cell, broken) in _breaks)
        {
            var elapsed = _time - broken.Start;
            if (elapsed < BreakBlinkDuration)
            {
                var phase = (int)(elapsed / BreakBlinkDuration * BreakBlinks * 2);
                if (phase % 2 == 0)
                {
                    DrawContainer(cell, broken.Look, Colors.White);
                }

                continue;
            }

            SpellFx.DrawSakura(this, new Vector2(cell.X * Tile, cell.Y * Tile), elapsed - BreakBlinkDuration);
        }

        var brazierSource = new Rect2(BrazierColumn, (int)(_time * 8) % FireFrames * FireFrameHeight, BrazierSize, BrazierSize);
        foreach (var brazier in _braziers)
        {
            DrawTextureRectRegion(
                _fire,
                new Rect2(brazier.X * Tile + BrazierOffsetX, brazier.Y * Tile + BrazierOffsetY, BrazierSize, BrazierSize),
                brazierSource);
        }

        foreach (var grille in _game.Grilles)
        {
            DrawDoor(grille, false);
        }

        foreach (var monster in _game.Monsters)
        {
            var color = monster.IsStunned ? StunnedColor : Colors.White;
            var glide = _monsterGlides.TryGetValue(monster.Id, out var value) ? value : ((Cell From, double Start)?)null;
            var shown = Shown(monster.Position, glide, out _);
            var alpha = SwapAlpha(monster.Id, ref shown);
            if (alpha <= 0f)
            {
                continue;
            }

            var source = new Rect2(monster.IsStunned ? 0 : frame * 32, MonsterRow(monster) * 32, 32, 32);
            if (Facing(monster.Id) > 0)
            {
                DrawSetTransform(new Vector2(shown.X + Tile, shown.Y), 0f, new Vector2(-1f, 1f));
                DrawTextureRectRegion(_animations, new Rect2(0, 0, Tile, Tile), source, new Color(color, alpha));
                DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
            }
            else
            {
                DrawTextureRectRegion(_animations, new Rect2(shown, Tile, Tile), source, new Color(color, alpha));
            }
        }

        DrawShocks();

        DrawAura(SpellFx.AuraBack);
        DrawMagus(_game.Magus.Position);
        DrawAura(SpellFx.AuraFront);

        foreach (var (tileId, position) in _ledges)
        {
            var source = TileSource(tileId);
            DrawTextureRectRegion(
                _tileset,
                new Rect2(position.X, position.Y + LedgeCut, SubTile, SubTile - LedgeCut),
                new Rect2(source.Position.X, source.Position.Y + LedgeCut, SubTile, SubTile - LedgeCut));
        }

        foreach (var grille in _game.Grilles)
        {
            DrawDoor(grille, true);
        }

        DrawZone(_game.Level);

        foreach (var cell in _highlights)
        {
            DrawRect(CellRect(cell), CandidateColor);
        }

        DrawFlashes();
        DrawHold();

        foreach (var grille in _game.Grilles)
        {
            if (grille.IsOpen)
            {
                var position = grille.Position;
                GuiAssets.DrawNumber(this, grille.TurnsRemaining, new Vector2(position.X * Tile + Tile / 2f, position.Y * Tile + DoorCounterOffsetY));
            }
        }

        foreach (var monster in _game.Monsters)
        {
            var position = monster.Position;
            if (monster.IsStunned)
            {
                SpellFx.DrawStun(this, new Vector2(position.X * Tile, position.Y * Tile), monster.StunTurns, _time);
            }
            else if (monster.IsMutating)
            {
                SpellFx.DrawMutation(this, new Vector2(position.X * Tile, position.Y * Tile), _time);
            }
        }
    }

    private void DrawContainer(Cell cell, Rect2 look, Color tint)
    {
        var target = new Rect2(
            cell.X * Tile + (Tile - look.Size.X) / 2f,
            cell.Y * Tile + Tile - look.Size.Y - ContainerBottomMargin,
            look.Size.X,
            look.Size.Y);
        DrawTextureRectRegion(_objects, target, look, tint);
    }

    private int MonsterRow(MonsterState monster)
    {
        var row = KindRow(monster.Kind);
        if (!monster.IsMutating || monster.IsStunned)
        {
            return row;
        }

        var other = KindRow(monster.Kind == MonsterKind.BlobVert ? MonsterKind.BlobRouge : MonsterKind.BlobVert);
        return (int)(_time / MutationBlinkDuration) % 2 == 0 ? row : other;
    }

    private static int KindRow(MonsterKind kind) => kind == MonsterKind.BlobRouge ? RedBlobRow : GreenBlobRow;

    private static Texture2D LoadTexture(string path) =>
        GD.Load<Texture2D>(path) ?? throw new InvalidOperationException($"Texture introuvable : {path}");

    private static Rect2 CellRect(Cell cell) => new(cell.X * Tile, cell.Y * Tile, Tile, Tile);

    private static Rect2 TileSource(int tileId) =>
        new(tileId % TilesetColumns * SubTile, tileId / TilesetColumns * SubTile, SubTile, SubTile);

    private static bool IsWalkGround(Terrain terrain) =>
        terrain is Terrain.Floor or Terrain.Stairs or Terrain.Brazier or Terrain.Water or Terrain.Barrel or Terrain.Crate;

    private static bool IsOpenGround(Terrain terrain) =>
        terrain is Terrain.Floor or Terrain.Stairs or Terrain.Grille or Terrain.Brazier or Terrain.Water or Terrain.Barrel or Terrain.Crate;

    private static int Pick(bool left, bool right, int firstTile) =>
        left && right ? firstTile + 1 : !left ? firstTile : firstTile + 2;

    private bool IsLit(LevelDefinition level, Cell cell)
    {
        if (_zone.Contains(cell))
        {
            return true;
        }

        var terrain = level.TerrainAt(cell);
        if (terrain is Terrain.Floor or Terrain.Stairs
            || (LevelDefinition.IsContainer(terrain) && _game is not null && !_game.IsContainer(cell)))
        {
            return false;
        }

        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (_zone.Contains(new Cell(cell.X + dx, cell.Y + dy)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void DrawZone(LevelDefinition level)
    {
        for (var y = 0; y < level.Height; y++)
        {
            for (var x = 0; x < level.Width; x++)
            {
                var darkness = _darkness[y * level.Width + x];
                if (darkness > 0.01f)
                {
                    DrawRect(CellRect(new Cell(x, y)), new Color(0f, 0f, 0f, darkness));
                }
            }
        }

        var alpha = _canCast ? 0.5f + 0.25f * Mathf.Sin((float)_time * 3f) : 0.35f;
        var color = new Color(_canCast ? ZoneEdgeColor : ZoneEdgeEmptyColor, alpha);
        foreach (var cell in _zone)
        {
            var left = cell.X * Tile;
            var top = cell.Y * Tile;
            if (IsOutsideEdge(level, new Cell(cell.X, cell.Y - 1)))
            {
                DrawLine(new Vector2(left, top + 0.5f), new Vector2(left + Tile, top + 0.5f), color, 1f);
            }

            if (IsOutsideEdge(level, new Cell(cell.X, cell.Y + 1)))
            {
                DrawLine(new Vector2(left, top + Tile - 0.5f), new Vector2(left + Tile, top + Tile - 0.5f), color, 1f);
            }

            if (IsOutsideEdge(level, new Cell(cell.X - 1, cell.Y)))
            {
                DrawLine(new Vector2(left + 0.5f, top), new Vector2(left + 0.5f, top + Tile), color, 1f);
            }

            if (IsOutsideEdge(level, new Cell(cell.X + 1, cell.Y)))
            {
                DrawLine(new Vector2(left + Tile - 0.5f, top), new Vector2(left + Tile - 0.5f, top + Tile), color, 1f);
            }
        }
    }

    private bool IsOutsideEdge(LevelDefinition level, Cell neighbour) =>
        !_zone.Contains(neighbour) && level.TerrainAt(neighbour) is Terrain.Floor or Terrain.Stairs;

    private void DrawHold()
    {
        if (_hold is not { } hold)
        {
            return;
        }

        var progress = (float)Math.Clamp((_time - hold.Start) / LongPressDuration, 0.0, 1.0);
        if (progress <= 0f)
        {
            return;
        }

        var start = -Mathf.Pi / 2f;
        DrawArc(CellCenter(hold.Cell), HoldRingRadius, start, start + Mathf.Tau * progress, HoldRingPoints, HoldRingColor, HoldRingWidth);
    }

    private void DrawFlashes()
    {
        foreach (var (cell, color, start) in _flashes)
        {
            var progress = (float)Math.Clamp((_time - start) / FlashDuration, 0.0, 1.0);
            var rect = CellRect(cell).Grow(FlashGrowth * progress);
            DrawRounded(rect, new Color(color, 0.45f * (1f - progress)), new Color(color, 1f - progress), 2);
        }
    }

    private void DrawRounded(Rect2 rect, Color fill, Color border, int borderWidth)
    {
        _roundedBox.BgColor = fill;
        _roundedBox.BorderColor = border;
        _roundedBox.SetBorderWidthAll(borderWidth);
        _roundedBox.SetCornerRadiusAll(FlashCornerRadius);
        DrawStyleBox(_roundedBox, rect);
    }

    private void DrawSubTile(int tileId, Vector2 position) =>
        DrawTextureRectRegion(_tileset, new Rect2(position, SubTile, SubTile), TileSource(tileId));

    private void DrawFire(Cell cell, Rect2 source) =>
        DrawTextureRectRegion(_fire, new Rect2(cell.X * Tile, cell.Y * Tile + FireOffsetY, Tile, FireFrameHeight), source);

    private void DrawWeb(Vector2 position, bool flipX)
    {
        if (!flipX)
        {
            DrawTexture(_web, position);
            return;
        }

        DrawSetTransform(new Vector2(position.X + WebSize, position.Y), 0f, new Vector2(-1f, 1f));
        DrawTexture(_web, Vector2.Zero);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    private bool IsThinDoor(Cell cell) => _thinDoors.Contains(cell);

    private void DrawDoor(GrilleState grille, bool foreground)
    {
        var frameTop = GrilleFrame(grille) * DoorFrameStride;
        var x = grille.Position.X * Tile;
        var y = grille.Position.Y * Tile;
        if (IsThinDoor(grille.Position))
        {
            var height = foreground ? SubTile : Tile + SubTile;
            DrawTextureRectRegion(_doors, new Rect2(x, y - SubTile, Tile, height), new Rect2(ThinDoorColumn, frameTop, Tile, height));
            return;
        }

        if (!foreground)
        {
            DrawTextureRectRegion(_doors, new Rect2(x, y, Tile, Tile), new Rect2(ThickDoorColumn, ThickDoorTop + frameTop, Tile, Tile));
        }
    }

    private int GrilleFrame(GrilleState grille)
    {
        var known = _grilleStates.TryGetValue(grille.Position, out var state);
        if (!known || state.Open != grille.IsOpen)
        {
            state = (grille.IsOpen, known ? _time : double.NegativeInfinity);
            _grilleStates[grille.Position] = state;
        }

        var progress = Math.Clamp((_time - state.ChangedAt) / GrilleAnimationDuration, 0.0, 1.0);
        var openness = state.Open ? progress : 1.0 - progress;
        return (int)Math.Round(openness * (GrilleFrames - 1));
    }

    private void BuildTerrain(LevelDefinition level)
    {
        _terrainTiles.Clear();
        _ledges.Clear();
        _stairs.Clear();
        _braziers.Clear();
        _water.Clear();
        _shading.Clear();
        _thinDoors.Clear();

        var width = level.Width * 2;
        var height = level.Height * 2;

        Terrain TerrainOf(int sx, int sy) =>
            sx >= 0 && sy >= 0 && sx < width && sy < height ? level.TerrainAt(new Cell(sx / 2, sy / 2)) : Terrain.Void;
        bool Floor(int sx, int sy) => IsWalkGround(TerrainOf(sx, sy));
        bool Door(int sx, int sy) => TerrainOf(sx, sy) == Terrain.Grille;
        bool FaceBottom(int sx, int sy) => !Floor(sx, sy) && Floor(sx, sy + 1);
        bool FaceTop(int sx, int sy) => !Floor(sx, sy) && !Floor(sx, sy + 1) && Floor(sx, sy + 2);
        bool Face(int sx, int sy) => FaceBottom(sx, sy) || FaceTop(sx, sy);
        bool Low(int sx, int sy) => Floor(sx, sy) || Face(sx, sy);
        bool ThinFace(int sx, int sy) => Floor(sx, (FaceTop(sx, sy) ? sy : sy - 1) - 1);
        bool ThinRun(int sx, int sy)
        {
            var bottom = FaceBottom(sx, sy);
            bool Same(int x) => bottom ? FaceBottom(x, sy) : FaceTop(x, sy);
            for (var x = sx; Same(x); x--)
            {
                if (!ThinFace(x, sy))
                {
                    return false;
                }
            }

            for (var x = sx + 1; Same(x); x++)
            {
                if (!ThinFace(x, sy))
                {
                    return false;
                }
            }

            return true;
        }

        bool ThinDoor(int sx, int sy) => Door(sx, sy) && ThinRun(sx, sy - (sy % 2 + 2) % 2 + 1);
        bool Ground(int sx, int sy) => Floor(sx, sy) || FaceBottom(sx, sy) || ThinDoor(sx, sy);
        bool Shaded(int sx, int sy) =>
            Ground(sx, sy)
            && (!Floor(sx, sy) || !Floor(sx + 1, sy) || !Floor(sx - 1, sy) || !Floor(sx, sy + 1) || !Floor(sx, sy - 1));
        bool Lit(int sx, int sy) => Floor(sx, sy) && !Shaded(sx, sy);
        int ShadeTile(int sx, int sy)
        {
            var n = Lit(sx, sy - 1);
            var s = Lit(sx, sy + 1);
            var w = Lit(sx - 1, sy);
            var e = Lit(sx + 1, sy);
            return n && w ? ShadeNorthWestTile
                : n && e ? ShadeNorthEastTile
                : s && w ? ShadeSouthWestTile
                : s && e ? ShadeSouthEastTile
                : n ? ShadeNorthTile
                : s ? ShadeSouthTile
                : w ? ShadeWestTile
                : e ? ShadeEastTile
                : Lit(sx + 1, sy + 1) ? ShadeInnerSouthEastTile
                : Lit(sx - 1, sy + 1) ? ShadeInnerSouthWestTile
                : Lit(sx + 1, sy - 1) ? ShadeInnerNorthEastTile
                : Lit(sx - 1, sy - 1) ? ShadeInnerNorthWestTile
                : ShadeCenterTile;
        }

        void AddGround(int sx, int sy, Vector2 position)
        {
            _terrainTiles.Add((FloorTile, position));
            if (Shaded(sx, sy))
            {
                var shade = ShadeTile(sx, sy);
                _shading[(sx, sy)] = shade;
                _terrainTiles.Add((shade, position));
            }
        }

        for (var sy = -1; sy < height; sy++)
        {
            for (var sx = 0; sx < width; sx++)
            {
                var position = new Vector2(sx * SubTile, sy * SubTile);
                if (Floor(sx, sy))
                {
                    AddGround(sx, sy, position);
                    if (FaceTop(sx, sy + 1) && !ThinDoor(sx, sy + 1))
                    {
                        var ledge = Floor(sx - 1, sy + 1) ? LedgeTile : Floor(sx + 1, sy + 1) ? LedgeTile + 2 : LedgeTile + 1;
                        _ledges.Add((ledge, position));
                    }

                    continue;
                }

                if (ThinDoor(sx, sy))
                {
                    AddGround(sx, sy, position);
                    continue;
                }

                if (Face(sx, sy))
                {
                    var bottom = FaceBottom(sx, sy);
                    if (bottom)
                    {
                        AddGround(sx, sy, position);
                    }

                    var first = bottom ? FaceBottomTile : FaceTopTile;
                    var tileId = Floor(sx - 1, sy) ? first
                        : Floor(sx + 1, sy) ? first + 2
                        : bottom && ThinRun(sx, sy) ? ThinFaceBottomTile
                        : first + 1;
                    _terrainTiles.Add((tileId, position));
                    continue;
                }

                var north = Floor(sx, sy - 1);
                var west = Low(sx - 1, sy);
                var east = Low(sx + 1, sy);
                int? top = FaceTop(sx, sy + 1)
                    ? Floor(sx - 1, sy) ? LedgeTile : Floor(sx + 1, sy) ? LedgeTile + 2 : LedgeTile + 1
                    : north && west ? TopNorthWestTile
                    : north && east ? TopNorthEastTile
                    : north ? TopNorthTile
                    : west ? FaceTop(sx - 1, sy) ? FaceBesideTopWestTile : FaceBottom(sx - 1, sy) ? FaceBesideBottomWestTile : TopWestTile
                    : east ? FaceTop(sx + 1, sy) ? FaceBesideTopEastTile : FaceBottom(sx + 1, sy) ? FaceBesideBottomEastTile : TopEastTile
                    : Floor(sx - 1, sy - 1) ? InnerCornerNorthWestTile
                    : Floor(sx + 1, sy - 1) ? InnerCornerNorthEastTile
                    : FaceTop(sx + 1, sy + 1) ? FaceCornerSouthEastTile
                    : FaceTop(sx - 1, sy + 1) ? FaceCornerSouthWestTile
                    : (int?)null;
                if (top is { } darkTile)
                {
                    _terrainTiles.Add((darkTile, position));
                }
            }
        }

        for (var y = 0; y < level.Height; y++)
        {
            for (var x = 0; x < level.Width; x++)
            {
                var cell = new Cell(x, y);
                if (level.TerrainAt(cell) == Terrain.Stairs)
                {
                    _stairs.Add(cell);
                }

                if (level.TerrainAt(cell) == Terrain.Brazier)
                {
                    _braziers.Add(cell);
                }

                if (level.TerrainAt(cell) == Terrain.Water)
                {
                    _water.Add(cell);
                }

                if (ThinDoor(x * 2, y * 2))
                {
                    _thinDoors.Add(cell);
                }
            }
        }
    }

    private void GenerateDecor(LevelDefinition level, Random random)
    {
        _cracks.Clear();
        _webs.Clear();
        _shields.Clear();
        _banners.Clear();
        _torches.Clear();
        _containerLooks.Clear();
        _breaks.Clear();

        bool IsFace(Cell cell) =>
            level.TerrainAt(cell) == Terrain.Wall && IsOpenGround(level.TerrainAt(new Cell(cell.X, cell.Y + 1)));

        var faces = new List<Cell>();
        for (var y = 0; y < level.Height; y++)
        {
            for (var x = 0; x < level.Width; x++)
            {
                var cell = new Cell(x, y);
                var terrain = level.TerrainAt(cell);
                if (terrain == Terrain.Floor)
                {
                    for (var quarter = 0; quarter < 4; quarter++)
                    {
                        if (random.NextDouble() >= CrackChance)
                        {
                            continue;
                        }

                        var sub = (X: x * 2 + quarter % 2, Y: y * 2 + quarter / 2);
                        var shaded = _shading.TryGetValue(sub, out var shade);
                        if (shaded && shade != ShadeCenterTile)
                        {
                            continue;
                        }

                        var center = new Vector2(sub.X * SubTile + SubTile / 2f, sub.Y * SubTile + SubTile / 2f);
                        var crack = CrackTiles[random.Next(CrackTiles.Length)] + (shaded ? ShadedCrackOffset : 0);
                        _cracks.Add((crack, center, random.Next(4)));
                    }
                }
                else if (IsFace(cell))
                {
                    faces.Add(cell);
                }
            }
        }

        _torches.AddRange(level.Torches);
        foreach (var container in _game?.Containers ?? Array.Empty<Cell>())
        {
            var looks = level.TerrainAt(container) == Terrain.Barrel ? BarrelRegions : CrateRegions;
            _containerLooks[container] = looks[random.Next(looks.Length)];
        }

        foreach (var face in faces)
        {
            var left = new Cell(face.X - 1, face.Y);
            var right = new Cell(face.X + 1, face.Y);
            if (level.TerrainAt(left) == Terrain.Wall && !IsFace(left) && random.NextDouble() < WebChance)
            {
                _webs.Add((new Vector2(face.X * Tile, face.Y * Tile + WebOffsetY), false));
            }

            if (level.TerrainAt(right) == Terrain.Wall && !IsFace(right) && random.NextDouble() < WebChance)
            {
                _webs.Add((new Vector2(face.X * Tile + Tile - WebSize, face.Y * Tile + WebOffsetY), true));
            }
        }

        foreach (var item in _decor)
        {
            var origin = new Vector2(item.Position.X * Tile, item.Position.Y * Tile);
            if (HangingRegions.TryGetValue(item.Id, out var hanging))
            {
                _banners.Add((hanging, new Vector2(origin.X, origin.Y + BannerOffsetY)));
            }
            else if (item.Id == ElementIds.Shield)
            {
                _shields.Add((ShieldRegion, new Rect2(origin.X + SubTile / 2f, origin.Y + ShieldOffsetY, SubTile, SubTile)));
            }
        }
    }

    private static int DirectionIndex(Cell delta) => (Math.Sign(delta.X), Math.Sign(delta.Y)) switch
    {
        (0, 1) => 0,
        (1, 1) => 1,
        (1, 0) => 2,
        (1, -1) => 3,
        (0, -1) => 4,
        (-1, -1) => 5,
        (-1, 0) => 6,
        (-1, 1) => 7,
        _ => 0,
    };

    private Vector2 Shown(Cell current, (Cell From, double Start)? glide, out bool moving)
    {
        moving = false;
        var target = new Vector2(current.X * Tile, current.Y * Tile);
        if (glide is not { } active)
        {
            return target;
        }

        var progress = (_time - active.Start) / MoveDuration;
        if (progress >= 1.0)
        {
            return target;
        }

        moving = true;
        var origin = new Vector2(active.From.X * Tile, active.From.Y * Tile);
        return origin.Lerp(target, (float)Math.Max(progress, 0.0));
    }

    private void DrawMagus(Cell cell)
    {
        var shown = Shown(cell, _magusGlide, out var moving);
        var alpha = 1f;
        if (_teleport is { } teleport && IsBusy)
        {
            var elapsed = _time - teleport.Start;
            alpha = SpellFx.TeleportMagusAlpha(elapsed);
            var at = SpellFx.IsDeparting(elapsed) ? teleport.From : teleport.To;
            shown = new Vector2(at.X * Tile, at.Y * Tile);
        }

        if (alpha <= 0f)
        {
            return;
        }

        var row = (moving ? WalkRowOffset : IdleRowOffset) + _magusDirection;
        var frame = moving ? (int)(_time * 12) % 6 : (int)(_time * 6) % 6;
        DrawTextureRectRegion(_magusPixel, new Rect2(shown, Tile, Tile), new Rect2(frame * 32, row * 32, 32, 32), new Color(1f, 1f, 1f, alpha));
    }

    private void DrawAura(Texture2D layer)
    {
        if (_teleport is not { } teleport || !IsBusy)
        {
            return;
        }

        var elapsed = _time - teleport.Start;
        var cell = SpellFx.IsDeparting(elapsed) ? teleport.From : teleport.To;
        SpellFx.DrawTeleportAura(this, layer, new Vector2(cell.X * Tile, cell.Y * Tile), elapsed);
    }

    private float SwapAlpha(int monsterId, ref Vector2 shown)
    {
        if (_swap is not { } swap || !IsBusy || !_swapOrigins.TryGetValue(monsterId, out var origin))
        {
            return 1f;
        }

        var alpha = SpellFx.SwapMonsterAlpha(_time - swap.Start, out var atOrigin);
        if (atOrigin)
        {
            shown = new Vector2(origin.X * Tile, origin.Y * Tile);
        }

        return alpha;
    }

    private void DrawShocks()
    {
        if (_swap is { } swap && IsBusy)
        {
            var (first, second) = SpellFx.SwapShockAlphas(_time - swap.Start);
            DrawShock(swap.First, first);
            DrawShock(swap.Second, second);
            return;
        }

        if (_selectionShock is { } selection)
        {
            DrawShock(selection.Cell, (float)Math.Clamp((_time - selection.Start) / SpellFx.ShockFadeInDuration, 0.0, 1.0));
        }

        if (_fadingShock is { } fading)
        {
            var alpha = 1f - (float)Math.Clamp((_time - fading.Start) / SpellFx.ShockSelectionFadeOutDuration, 0.0, 1.0);
            if (alpha <= 0f)
            {
                _fadingShock = null;
                return;
            }

            DrawShock(fading.Cell, alpha);
        }
    }

    private void DrawShock(Cell cell, float alpha) =>
        SpellFx.DrawShock(this, new Vector2(cell.X * Tile, cell.Y * Tile), alpha, _time);

}
