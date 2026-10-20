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
        new(55, 84, 20, 28), new(80, 82, 16, 20), new(96, 80, 16, 24), new(160, 82, 16, 20), new(176, 80, 16, 24),
    };
    private static readonly Rect2[] BarrelRegions = { new(112, 80, 16, 24), new(192, 80, 16, 24) };
    private static readonly Rect2[] VaseRegions =
    {
        new(208, 80, 16, 19), new(256, 80, 16, 19), new(224, 83, 16, 16), new(272, 83, 16, 16),
    };
    private static readonly Rect2 LeverRegion = new(0, 0, 16, 12);
    private const int LeverFrameStride = 16;
    private const int LeverBottomMargin = 6;
    private static readonly int[] AlcoveTiles = { 425, 426, 442, 443, 459, 460 };
    private static readonly int[] CandleTiles = { 12, 13, 18, 19 };
    private const int CandleColumns = 6;
    private const int CandleFrameStride = 24;
    private const int CandleFrames = 3;
    private const double CandleFrameSeconds = 0.15;
    private const int AuraSize = 48;
    private const int AuraOffsetX = -7;
    private const int AuraOffsetY = -12;
    private const double AuraGrowSeconds = 0.4;
    private const int SparkleFrames = 4;
    private const double SparkleFrameRate = 6;
    private const int SparkleSize = 32;
    private static readonly Rect2 StairsUpRegion = new(64, 16, 48, 64);
    private const int StairsUpRiseY = 3 * SubTile;
    private const int CoastColumns = 29;
    private const int ReflectionColumns = 37;
    private const int ReflectionFrameStride = 481;
    private const int WaterFrames = 6;
    private const double WaterFrameSeconds = 0.15;
    private const int ReflectionShownFrames = 14;
    private const int ReflectionCycleFrames = 15;
    private const int ReflectionLayers = 2;
    private const double ReflectionCoverage = 0.95;
    private const int ReflectionAttempts = 20000;
    private const int FoamTile = -1;
    private const int WaterTile = 881;
    private const int BandTile = 875;
    private const int BandWestTile = 871;
    private const int BandEastTile = 873;
    private const int SlabTile = 263;
    private const int SlabWestTile = 262;
    private const int SlabEastTile = 264;
    private const int WaterFaceTile = 118;
    private const int WaterFaceWestTile = 117;
    private const int WaterFaceEastTile = 119;
    private const int FaceFootTile = 147;
    private const int FaceFootWestTile = 146;
    private const int FaceFootEastTile = 148;
    private const int WallWaterEastTile = 240;
    private const int WallWaterWestTile = 242;
    private const int WallWaterEastBandTile = 245;
    private const int WallWaterWestBandTile = 244;
    private const int BankWestBandTile = 155;
    private const int BankEastBandTile = 153;
    private const int NoShade = -1;
    private const int BankNorthWestTile = 175;
    private const int BankNorthTile = 176;
    private const int BankNorthEastTile = 177;
    private const int BankWestTile = 204;
    private const int BankEastTile = 206;
    private const int BankSouthWestTile = 233;
    private const int BankSouthTile = 234;
    private const int BankSouthEastTile = 235;
    private const int BankCornerNorthEastTile = 266;
    private const int BankCornerNorthWestTile = 268;
    private const int BankCornerSouthEastTile = 150;
    private const int BankCornerSouthWestTile = 152;
    private static readonly Dictionary<int, int[]> CoastFrames = new()
    {
        [117] = new[] { 117, 131, 407, 421, 697, 711 },
        [119] = new[] { 119, 133, 409, 423, 699, 713 },
        [146] = new[] { 146, 160, 436, 450, 726, 740 },
        [147] = new[] { 147, 161, 437, 451, 727, 741 },
        [148] = new[] { 148, 162, 438, 452, 728, 742 },
        [175] = new[] { 175, 189, 465, 479, 755, 769 },
        [177] = new[] { 177, 191, 467, 481, 757, 771 },
        [204] = new[] { 204, 218, 494, 508, 784, 798 },
        [206] = new[] { 206, 220, 496, 510, 786, 800 },
        [153] = new[] { 153, 167, 443, 457, 733, 747 },
        [155] = new[] { 155, 169, 445, 459, 735, 749 },
        [244] = new[] { 244, 258, 534, 548, 824, 838 },
        [245] = new[] { 245, 259, 535, 549, 825, 839 },
        [233] = new[] { 233, 247, 523, 537, 813, 827 },
        [235] = new[] { 235, 249, 525, 539, 815, 829 },
        [240] = new[] { 240, 254, 530, 544, 820, 834 },
        [242] = new[] { 242, 256, 532, 546, 822, 836 },
        [262] = new[] { 262, 276, 552, 566, 842, 856 },
        [263] = new[] { 263, 277, 553, 567, 727, 741 },
        [264] = new[] { 264, 278, 554, 568, 844, 858 },
    };
    private static readonly int[][] ReflectionStamps =
    {
        new[] { 0, 0, 55, 0, 1, 92, 0, 2, 129, 1, 0, 56, 1, 1, 93, 1, 2, 130, 2, 0, 57, 2, 1, 94, 2, 2, 131, 3, 0, 58, 3, 1, 95, 3, 2, 132, 4, 0, 59, 4, 1, 96, 4, 2, 133, 5, 0, 60, 5, 1, 97, 5, 2, 134, 6, 0, 61, 6, 1, 98, 6, 2, 135, 7, 0, 62, 7, 1, 99, 7, 2, 136, 8, 0, 63, 8, 1, 100, 8, 2, 137, 9, 0, 64, 9, 1, 101, 9, 2, 138, 10, 0, 65, 10, 1, 102, 10, 2, 139, 11, 0, 66, 11, 1, 103, 11, 2, 140, 12, 0, 67, 12, 1, 104, 12, 2, 141 },
        new[] { 0, 0, 323, 0, 1, 360, 0, 2, 397, 1, 0, 324, 1, 1, 361, 1, 2, 398, 2, 0, 325, 2, 1, 362, 2, 2, 399, 3, 0, 326, 3, 1, 363, 3, 2, 400, 4, 0, 327, 4, 1, 364, 4, 2, 401 },
        new[] { 0, 0, 254, 1, 0, 255, 2, 0, 256, 3, 0, 257, 4, 0, 258 },
        new[] { 0, 0, 325, 0, 1, 362, 1, 0, 326, 1, 1, 363, 2, 0, 327, 2, 1, 364 },
        new[] { 0, 0, 341, 0, 1, 378, 1, 0, 342, 1, 1, 379, 2, 0, 343, 2, 1, 380, 3, 0, 344, 3, 1, 381, 4, 0, 345, 4, 1, 382, 5, 0, 346, 5, 1, 383, 6, 0, 347, 6, 1, 384 },
        new[] { 0, 0, 323, 0, 1, 360, 1, 0, 324, 1, 1, 361, 2, 0, 325, 2, 1, 362, 3, 0, 326, 3, 1, 363, 4, 0, 327, 4, 1, 364 },
        new[] { 0, 0, 105, 1, 0, 106, 2, 0, 107, 3, 0, 108, 4, 0, 109, 5, 0, 110 },
        new[] { 0, 0, 313, 0, 1, 350, 0, 2, 387, 1, 0, 314, 1, 1, 351, 1, 2, 388, 2, 0, 315, 2, 1, 352, 2, 2, 389, 3, 0, 316, 3, 1, 353, 3, 2, 390, 4, 0, 317, 4, 1, 354, 4, 2, 391, 5, 0, 318, 5, 1, 355, 5, 2, 392 },
        new[] { 0, 0, 255, 1, 0, 256, 2, 0, 257 },
        new[] { 0, 0, 211, 0, 1, 248, 1, 0, 212, 1, 1, 249, 2, 0, 213, 2, 1, 250, 3, 0, 214, 3, 1, 251, 4, 0, 215, 4, 1, 252 },
        new[] { 0, 0, 55, 0, 1, 92, 0, 2, 129, 1, 0, 56, 1, 1, 93, 1, 2, 130, 2, 0, 57, 2, 1, 94, 2, 2, 131, 3, 0, 58, 3, 1, 95, 3, 2, 132, 4, 0, 59, 4, 1, 96, 4, 2, 133, 5, 0, 60, 5, 1, 97, 5, 2, 134, 6, 0, 61, 6, 1, 98, 6, 2, 135, 7, 0, 62, 7, 1, 99, 7, 2, 136, 8, 0, 63, 8, 1, 100, 8, 2, 137, 9, 0, 64, 9, 1, 101, 9, 2, 138, 10, 0, 65, 10, 1, 102, 10, 2, 139, 11, 0, 66, 11, 1, 103, 11, 2, 140 },
    };
    private const int PillarCapTile = 257;
    private const int PillarUpperTile = 274;
    private const int PillarLowerTile = 291;
    private const int PillarEndTile = 376;
    private const int PillarBaseTile = 393;
    private const int PilasterLedgeTile = 304;
    private const int PilasterTopTile = 321;
    private const int PilasterBottomTile = 338;
    private const int CrossTopTile = 308;
    private const int CrossMiddleTile = 325;
    private const int CrossBottomTile = 342;
    private const int PillarFrom = 4;
    private const int PillarTo = 13;
    private const int NotchFrom = 2;
    private const int NotchTo = 14;
    private const int BlockWestTopTile = 306;
    private const int BlockTopLeftTile = 307;
    private const int BlockTopRightTile = 309;
    private const int BlockEastTopTile = 310;
    private const int BlockWestMiddleTile = 323;
    private const int BlockMiddleLeftTile = 324;
    private const int BlockMiddleRightTile = 326;
    private const int BlockEastMiddleTile = 327;
    private const int BlockWestBottomTile = 340;
    private const int BlockBottomLeftTile = 341;
    private const int BlockBottomRightTile = 343;
    private const int BlockEastBottomTile = 344;
    private const int CornerWestTopTile = 311;
    private const int CornerWestMiddleTile = 328;
    private const int CornerWestBottomTile = 345;
    private const int CornerEastTopTile = 312;
    private const int CornerEastMiddleTile = 329;
    private const int CornerEastBottomTile = 346;
    private const int PillarWestOffset = 8;
    private const int PillarCenterOffset = 16;
    private const int PillarEastOffset = 24;
    private const int ContainerBottomMargin = 2;
    private const double BreakBlinkDuration = 0.25;
    private const int BreakBlinks = 3;
    public const double PotionAppearDelay = 0.3;
    private const double PotionBounceDuration = 0.35;
    private const float PotionBounceHeight = 8f;

    private const double CrackChance = 0.4;
    private const double WebChance = 0.5;
    private const double GrilleAnimationDuration = 0.35;
    private const int ChestFrames = 5;
    private const double ChestAnimationDuration = 0.3;
    private const int SpikeFrames = 5;
    private const double SpikeAnimationDuration = 0.15;
    private const float KeyBob = 1f;
    private const int BloodFrameSize = 96;
    private const int BloodColumns = 5;
    private const int BloodFrames = 7;
    private const double BloodFrameDuration = 0.08;
    private const double ImpaleHold = 0.6;
    private const float ScrollLift = 21f;
    private static readonly Rect2 KeyRegion = new(0, 0, 14, 9);
    private static readonly Rect2[] CoinRegions = { new(24, 0, 21, 19), new(48, 0, 17, 15), new(72, 0, 12, 9) };
    private static readonly Rect2 AltarRegion = new(39, 129, 50, 30);
    private const int GrilleFrames = 5;
    private const int DoorFrameStride = 48;
    private const int ThickDoorColumn = 32;
    private const int ThickDoorTop = 16;
    private const int ThinDoorColumn = 96;
    private const int ThickGrilleColumn = 0;
    private const int ThinGrilleColumn = 64;
    private const int WebSize = 16;
    private const int WebOffsetY = -2;
    private const int ShieldOffsetY = 5;
    private const int FireFrames = 6;
    private const int FireFrameHeight = 48;
    private const int FireColumn = 39;
    private const int FireOffsetY = -11;
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
    private const int GreenBlobRow = 5;
    private const int RedBlobRow = 6;
    private const int BatRow = 9;
    private const int BatFrames = 3;
    private const double BatFrameRate = 8;
    private const int BatRestingFrame = 2;
    private const float BatHover = 1.5f;
    private const float BatHoverSpeed = 4f;
    private const float BatLift = -3f;
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
    private readonly List<(int TileId, int ShadeId, Vector2 Position)> _waterUnder = new();
    private readonly Dictionary<(int TileId, int ShadeId), ImageTexture> _shadedCoasts = new();
    private Image? _coastImage;
    private Image? _tilesetImage;
    private Color _floorColor;
    private readonly List<(int TileId, Vector2 Position)> _waterOver = new();
    private readonly HashSet<(int X, int Y)> _reflectionArea = new();
    private readonly List<(int TileId, Vector2 Position)>[] _reflections = { new(), new() };
    private readonly List<Cell> _levers = new();
    private readonly Dictionary<Cell, (bool Open, double ChangedAt)> _chestStates = new();
    private readonly Dictionary<Cell, (bool Raised, double ChangedAt)> _spikeStates = new();
    private readonly List<(Rect2 Source, Vector2 Position)> _coins = new();
    private readonly List<(Cell First, int Width)> _altars = new();
    private readonly List<Cell> _alcoves = new();
    private readonly List<Cell> _stairsUp = new();
    private readonly List<Cell> _arches = new();
    private readonly Dictionary<Cell, bool> _parois = new();
    private readonly List<ParoiTile> _paroiTiles = new();
    private readonly List<ParoiTile> _paroiFront = new();
    private readonly List<Cell> _frontTorches = new();
    private readonly Dictionary<Cell, Rect2> _containerLooks = new();
    private readonly Dictionary<Cell, (double Start, Rect2 Look)> _breaks = new();
    private readonly HashSet<Cell> _thinDoors = new();
    private readonly Dictionary<Cell, (bool Open, double ChangedAt)> _grilleStates = new();
    private readonly Dictionary<Cell, double> _alcoveLitAt = new();
    private Texture2D _tileset = null!;
    private Texture2D _doors = null!;
    private Texture2D _fire = null!;
    private Texture2D _objects = null!;
    private Texture2D _items = null!;
    private Texture2D _web = null!;
    private Texture2D _animations = null!;
    private Texture2D _potion = null!;
    private Texture2D _magusPixel = null!;
    private Texture2D _coastSheet = null!;
    private Texture2D _reflectionSheet = null!;
    private Texture2D _foamSheet = null!;
    private Texture2D _candleSheet = null!;
    private Texture2D _unlitCandles = null!;
    private Texture2D _auraSheet = null!;
    private Texture2D _sparkleSheet = null!;
    private Texture2D _chestSheet = null!;
    private Texture2D _spikeSheet = null!;
    private Texture2D _treasures = null!;
    private Texture2D _scroll = null!;
    private Texture2D _blood = null!;
    private (Cell Cell, double Start)? _impale;
    private Texture2D _mechanisms = null!;
    private TorchAmbience _ambience = null!;
    private TorchAmbience _waterAmbience = null!;
    private BatAmbience _batAmbience = null!;
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
        || (_impale is { } impale && _time - impale.Start < ImpaleDuration)
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

    public const double MoveDuration = 0.15;
    private const int DefaultFacing = -1;
    private const float HoldRingRadius = 14f;
    private const float HoldRingWidth = 2f;
    private const int HoldRingPoints = 32;
    private static readonly Color HoldRingColor = new(0.95f, 0.89f, 0.74f, 0.9f);

    public const double LongPressDuration = 0.5;
    private const int WalkRowOffset = 0;
    private const int IdleRowOffset = 8;

    private IEnumerable<(int Id, Vector2 Position)> Bats() =>
        _game?.Monsters
            .Where(monster => monster.Kind == MonsterKind.ChauveSouris && !monster.IsStunned)
            .Select(monster => (monster.Id, CellCenter(monster.Position)))
        ?? Enumerable.Empty<(int, Vector2)>();

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
        _coastSheet = LoadTexture("res://assets/sprites/eau_berges.png");
        _reflectionSheet = LoadTexture("res://assets/sprites/eau_reflets.png");
        _foamSheet = LoadTexture("res://assets/sprites/eau_ecume.png");
        _candleSheet = LoadTexture("res://assets/sprites/bougies.png");
        _unlitCandles = LoadTexture("res://assets/sprites/bougies_eteintes.png");
        _auraSheet = LoadTexture("res://assets/sprites/aura_brasero.png");
        _sparkleSheet = LoadTexture("res://assets/sprites/etincelles.png");
        _chestSheet = LoadTexture("res://assets/sprites/coffre.png");
        _spikeSheet = LoadTexture("res://assets/sprites/piege.png");
        _treasures = LoadTexture("res://assets/sprites/tresors.png");
        _scroll = LoadTexture("res://assets/sprites/parchemin.png");
        _blood = LoadTexture("res://assets/fx/sang.png");
        _mechanisms = LoadTexture("res://assets/sprites/donjon_mecanismes.png");
        _ambience = new TorchAmbience { Name = "Torches", Listener = () => MagusFocus };
        AddChild(_ambience);
        _waterAmbience = new TorchAmbience { Name = "Eau", Listener = () => MagusFocus, SoundKey = "dc.water", MaxVoices = 1 };
        AddChild(_waterAmbience);
        _batAmbience = new BatAmbience { Name = "ChauvesSouris", Listener = () => MagusFocus, Bats = Bats };
        AddChild(_batAmbience);
    }

    public void ShowLevel(Game game, IReadOnlyList<DecorItem> decor)
    {
        _game = game;
        _decor = decor;
        BuildTerrain(game.Level);
        GenerateDecor(game.Level, new Random());
        _grilleStates.Clear();
        _alcoveLitAt.Clear();
        _chestStates.Clear();
        _spikeStates.Clear();
        _impale = null;
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
        Sounds.CancelPending();
        _ambience.ShowSources(game.Level.Torches.Concat(_braziers));
        _waterAmbience.ShowSources(_water);
        _batAmbience.Reset();
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
        MoveObjectLooks(events.OfType<ObjectMoved>().ToList());
        if (events.OfType<MagusImpaled>().FirstOrDefault() is { } impaled)
        {
            _impale = (impaled.Position, _time + LevelView.MoveDuration);
        }
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
        TurnSounds.Play(events, monstersStart - _time, _game?.Level, _game?.Magus.Position);
    }

    private void MoveObjectLooks(IReadOnlyList<ObjectMoved> moves)
    {
        if (moves.Count == 0)
        {
            return;
        }

        var looks = moves.Where(move => _containerLooks.ContainsKey(move.From)).Select(move => (move.To, Look: _containerLooks[move.From])).ToList();
        var chests = moves.Where(move => _chestStates.ContainsKey(move.From)).Select(move => (move.To, State: _chestStates[move.From])).ToList();
        foreach (var move in moves)
        {
            _containerLooks.Remove(move.From);
            _chestStates.Remove(move.From);
        }

        foreach (var (to, look) in looks)
        {
            _containerLooks[to] = look;
        }

        foreach (var (to, state) in chests)
        {
            _chestStates[to] = (state.Open, double.NegativeInfinity);
        }
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

        DrawWater();

        foreach (var tile in _paroiTiles)
        {
            DrawParoiTile(tile);
        }

        foreach (var spikes in _game.SpikeCells)
        {
            var spikeFrame = SpikeFrame(spikes);
            var spikeTarget = CellRect(spikes);
            if (AboveThinWall(spikes))
            {
                spikeTarget.Position -= new Vector2(0, SubTile / 2f);
            }

            DrawTextureRectRegion(_spikeSheet, spikeTarget, new Rect2(spikeFrame * Tile, 0, Tile, Tile));
        }

        foreach (var stairs in _stairs)
        {
            DrawTextureRectRegion(_objects, CellRect(stairs), StairsRegion);
        }

        foreach (var stairs in _stairsUp)
        {
            var target = new Rect2(stairs.X * Tile + Tile - StairsUpRegion.Size.X, stairs.Y * Tile - StairsUpRiseY, StairsUpRegion.Size.X, StairsUpRegion.Size.Y);
            if (DescendsEast(_game.Level, stairs))
            {
                var left = stairs.X * Tile;
                DrawSetTransform(new Vector2(left * 2 + Tile, 0), 0f, new Vector2(-1f, 1f));
                DrawTextureRectRegion(_objects, new Rect2(left + Tile - StairsUpRegion.Size.X, target.Position.Y, target.Size.X, target.Size.Y), StairsUpRegion);
                DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
            }
            else
            {
                DrawTextureRectRegion(_objects, target, StairsUpRegion);
            }
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

        foreach (var (source, position) in _coins)
        {
            DrawTextureRectRegion(_treasures, new Rect2(position, source.Size), source);
        }

        foreach (var key in _game.Keys)
        {
            var bob = Mathf.Round(Mathf.Sin((float)_time * 3f) * KeyBob);
            DrawTextureRectRegion(
                _treasures,
                new Rect2(key.X * Tile + (Tile - KeyRegion.Size.X) / 2f, key.Y * Tile + (Tile - KeyRegion.Size.Y) / 2f + bob, KeyRegion.Size.X, KeyRegion.Size.Y),
                KeyRegion);
        }

        if (_game.Monsters.Any(monster => monster.Kind is MonsterKind.BlobVert or MonsterKind.BlobRouge))
        {
            foreach (var cell in _game.FearZone(MonsterKind.BlobVert))
            {
                DrawRect(CellRect(cell), FireFearColor);
            }

            foreach (var cell in _game.FearZone(MonsterKind.BlobRouge))
            {
                DrawRect(CellRect(cell), WaterFearColor);
            }
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

        foreach (var chest in _game.Chests.Keys)
        {
            var chestFrame = ChestFrame(chest);
            DrawTextureRectRegion(_chestSheet, new Rect2(chest.X * Tile, chest.Y * Tile - ContainerBottomMargin, Tile, Tile), new Rect2(chestFrame * Tile, 0, Tile, Tile));
        }

        foreach (var (first, width) in _altars)
        {
            var altar = AltarRect(first, width);
            DrawTextureRectRegion(_items, altar, AltarRegion);
            if (!_game.CanSwapObjects && _game.Script is not null)
            {
                var center = new Vector2(altar.Position.X + altar.Size.X / 2f, altar.Position.Y + altar.Size.Y - ScrollLift);
                DrawTexture(_scroll, new Vector2(Mathf.Round(center.X - _scroll.GetWidth() / 2f), Mathf.Round(center.Y - _scroll.GetHeight() / 2f)));
                var sparkle = new Rect2((int)(_time * SparkleFrameRate) % SparkleFrames * SparkleSize, 0, SparkleSize, SparkleSize);
                DrawTextureRectRegion(_sparkleSheet, new Rect2(center.X - SparkleSize / 2f, center.Y - SparkleSize / 2f, SparkleSize, SparkleSize), sparkle);
            }
        }

        if (_game.PotionsAreRevealed)
        {
            var sparkle = new Rect2((int)(_time * SparkleFrameRate) % SparkleFrames * SparkleSize, 0, SparkleSize, SparkleSize);
            foreach (var container in _game.Containers.Where(_game.ContainerHasPotion))
            {
                if (_containerLooks.TryGetValue(container, out var look))
                {
                    var center = ContainerRect(container, look).GetCenter();
                    DrawTextureRectRegion(
                        _sparkleSheet,
                        new Rect2(center.X - SparkleSize / 2f, center.Y - SparkleSize / 2f, SparkleSize, SparkleSize),
                        sparkle);
                }
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

        foreach (var lever in _levers)
        {
            DrawLever(lever);
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

        foreach (var arch in _arches)
        {
            DrawArch(arch, false);
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

            var shownFrame = monster.IsStunned
                ? monster.Kind == MonsterKind.ChauveSouris ? BatRestingFrame : 0
                : MonsterFrame(monster, frame);
            var source = new Rect2(shownFrame * 32, MonsterRow(monster) * 32, 32, 32);
            if (monster.Kind == MonsterKind.ChauveSouris && !monster.IsStunned)
            {
                shown.Y += BatLift + Mathf.Round(Mathf.Sin((float)_time * BatHoverSpeed + monster.Id) * BatHover);
            }

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
        DrawBlood();

        foreach (var (tileId, position) in _ledges)
        {
            var source = TileSource(tileId);
            DrawTextureRectRegion(
                _tileset,
                new Rect2(position.X, position.Y + LedgeCut, SubTile, SubTile - LedgeCut),
                new Rect2(source.Position.X, source.Position.Y + LedgeCut, SubTile, SubTile - LedgeCut));
        }

        foreach (var tile in _paroiFront)
        {
            DrawParoiTile(tile);
        }

        foreach (var alcove in _alcoves)
        {
            DrawAlcove(alcove);
        }

        foreach (var torch in _frontTorches)
        {
            DrawFire(torch, fireSource);
        }

        foreach (var grille in _game.Grilles)
        {
            DrawDoor(grille, true);
        }

        foreach (var arch in _arches)
        {
            DrawArch(arch, true);
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

    private void DrawContainer(Cell cell, Rect2 look, Color tint) =>
        DrawTextureRectRegion(_objects, ContainerRect(cell, look), look, tint);

    private static Rect2 ContainerRect(Cell cell, Rect2 look) =>
        new(
            cell.X * Tile + (Tile - look.Size.X) / 2f,
            cell.Y * Tile + Tile - look.Size.Y - ContainerBottomMargin,
            look.Size.X,
            look.Size.Y);

    private void DrawAlcove(Cell alcove)
    {
        var origin = new Vector2(alcove.X * Tile, alcove.Y * Tile - SubTile);
        for (var index = 0; index < AlcoveTiles.Length; index++)
        {
            DrawSubTile(AlcoveTiles[index], origin + new Vector2(index % 2 * SubTile, index / 2 * SubTile));
        }

        if (_game is null || !_game.IsActivated(alcove))
        {
            _alcoveLitAt[alcove] = double.PositiveInfinity;
            DrawTexture(_unlitCandles, new Vector2(alcove.X * Tile, alcove.Y * Tile));
            return;
        }

        if (!_alcoveLitAt.TryGetValue(alcove, out var litAt) || double.IsPositiveInfinity(litAt))
        {
            litAt = _alcoveLitAt.ContainsKey(alcove) ? _time : double.NegativeInfinity;
            _alcoveLitAt[alcove] = litAt;
        }

        var growth = Math.Clamp((_time - litAt) / AuraGrowSeconds, 0.0, 1.0);
        var auraSize = AuraSize * (float)(1.0 - (1.0 - growth) * (1.0 - growth));
        var auraCenter = new Vector2(alcove.X * Tile + AuraOffsetX + AuraSize / 2f, alcove.Y * Tile + AuraOffsetY + AuraSize / 2f);
        var auraFrame = (int)(_time * 8) % FireFrames;
        DrawTextureRectRegion(
            _auraSheet,
            new Rect2(auraCenter.X - auraSize / 2f, auraCenter.Y - auraSize / 2f, auraSize, auraSize),
            new Rect2(auraFrame * AuraSize, 0, AuraSize, AuraSize));

        var stride = (int)(_time / CandleFrameSeconds) % CandleFrames * CandleFrameStride;
        for (var index = 0; index < CandleTiles.Length; index++)
        {
            var id = CandleTiles[index] + stride;
            DrawTextureRectRegion(
                _candleSheet,
                new Rect2(alcove.X * Tile + index % 2 * SubTile, alcove.Y * Tile + index / 2 * SubTile, SubTile, SubTile),
                new Rect2(id % CandleColumns * SubTile, id / CandleColumns * SubTile, SubTile, SubTile));
        }
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

    private static int KindRow(MonsterKind kind) => kind switch
    {
        MonsterKind.BlobRouge => RedBlobRow,
        MonsterKind.ChauveSouris => BatRow,
        _ => GreenBlobRow,
    };

    private int MonsterFrame(MonsterState monster, int blobFrame) =>
        monster.Kind == MonsterKind.ChauveSouris ? ((int)(_time * BatFrameRate) + monster.Id) % BatFrames : blobFrame;

    private static Texture2D LoadTexture(string path) =>
        GD.Load<Texture2D>(path) ?? throw new InvalidOperationException($"Texture introuvable : {path}");

    private static Rect2 CellRect(Cell cell) => new(cell.X * Tile, cell.Y * Tile, Tile, Tile);

    private static Rect2 TileSource(int tileId) =>
        new(tileId % TilesetColumns * SubTile, tileId / TilesetColumns * SubTile, SubTile, SubTile);

    private static bool IsWalkGround(Terrain terrain) =>
        terrain is Terrain.Floor or Terrain.Stairs or Terrain.Brazier or Terrain.Water or Terrain.Barrel or Terrain.Crate
            or Terrain.Vase or Terrain.Lever or Terrain.StairsUp or Terrain.Chest or Terrain.Spikes or Terrain.Altar;

    private static bool IsOpenGround(Terrain terrain) =>
        terrain is Terrain.Floor or Terrain.Stairs or Terrain.Grille or Terrain.Arch or Terrain.Brazier or Terrain.Water or Terrain.Barrel
            or Terrain.Crate or Terrain.Vase or Terrain.Lever or Terrain.StairsUp or Terrain.Chest or Terrain.Spikes or Terrain.Altar;

    private static int Pick(bool left, bool right, int firstTile) =>
        left && right ? firstTile + 1 : !left ? firstTile : firstTile + 2;

    private bool IsLit(LevelDefinition level, Cell cell)
    {
        if (_zone.Contains(cell))
        {
            return true;
        }

        var terrain = level.TerrainAt(cell);
        var holdsObject = _game is not null && _game.IsBigObject(cell);
        if (!holdsObject
            && (terrain is Terrain.Floor or Terrain.Stairs or Terrain.Chest || LevelDefinition.IsContainer(terrain)))
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

    private void DrawWater()
    {
        var frame = (int)(_time / WaterFrameSeconds);
        foreach (var (tileId, shadeId, position) in _waterUnder)
        {
            if (shadeId == NoShade)
            {
                DrawCoast(tileId, position, frame);
            }
            else
            {
                DrawTexture(ShadedCoast(CoastFrame(tileId, frame), shadeId), position);
            }
        }

        var cycle = frame % (ReflectionCycleFrames * ReflectionLayers);
        if (cycle % ReflectionCycleFrames < ReflectionShownFrames)
        {
            var stride = ReflectionFrameStride * (frame % WaterFrames);
            foreach (var (tileId, position) in _reflections[cycle / ReflectionCycleFrames])
            {
                var id = tileId + stride;
                DrawTextureRectRegion(
                    _reflectionSheet,
                    new Rect2(position, SubTile, SubTile),
                    new Rect2(id % ReflectionColumns * SubTile, id / ReflectionColumns * SubTile, SubTile, SubTile));
            }
        }

        foreach (var (tileId, position) in _waterOver)
        {
            if (tileId == FoamTile)
            {
                DrawTextureRectRegion(_foamSheet, new Rect2(position, SubTile, SubTile), new Rect2(frame % WaterFrames * SubTile, 0, SubTile, SubTile));
            }
            else
            {
                DrawCoast(tileId, position, frame);
            }
        }
    }

    private static int CoastFrame(int tileId, int frame) =>
        CoastFrames.TryGetValue(tileId, out var frames) ? frames[frame % frames.Length] : tileId;

    private ImageTexture ShadedCoast(int coastId, int shadeId)
    {
        if (_shadedCoasts.TryGetValue((coastId, shadeId), out var cached))
        {
            return cached;
        }

        _coastImage ??= Readable(_coastSheet.GetImage());
        if (_tilesetImage is null)
        {
            _tilesetImage = Readable(_tileset.GetImage());
            _floorColor = DominantColor(_tilesetImage, TileSource(FloorTile));
        }

        var coast = _coastImage.GetRegion(new Rect2I(coastId % CoastColumns * SubTile, coastId / CoastColumns * SubTile, SubTile, SubTile));
        var shade = _tilesetImage.GetRegion(PixelRect(TileSource(shadeId)));
        coast.Convert(Image.Format.Rgba8);
        for (var y = 0; y < SubTile; y++)
        {
            for (var x = 0; x < SubTile; x++)
            {
                var shadow = shade.GetPixel(x, y);
                var pixel = coast.GetPixel(x, y);
                if (shadow.A <= 0f || pixel.A <= 0f)
                {
                    continue;
                }

                coast.SetPixel(x, y, new Color(
                    Mathf.Min(1f, pixel.R * shadow.R / Mathf.Max(_floorColor.R, 0.001f)),
                    Mathf.Min(1f, pixel.G * shadow.G / Mathf.Max(_floorColor.G, 0.001f)),
                    Mathf.Min(1f, pixel.B * shadow.B / Mathf.Max(_floorColor.B, 0.001f)),
                    pixel.A));
            }
        }

        var texture = ImageTexture.CreateFromImage(coast);
        _shadedCoasts[(coastId, shadeId)] = texture;
        return texture;
    }

    private static Rect2I PixelRect(Rect2 region) =>
        new((int)region.Position.X, (int)region.Position.Y, (int)region.Size.X, (int)region.Size.Y);

    private static Image Readable(Image image)
    {
        if (image.IsCompressed())
        {
            image.Decompress();
        }

        return image;
    }

    private static Color DominantColor(Image image, Rect2 region)
    {
        var counts = new Dictionary<Color, int>();
        var area = PixelRect(region);
        for (var y = area.Position.Y; y < area.End.Y; y++)
        {
            for (var x = area.Position.X; x < area.End.X; x++)
            {
                var color = image.GetPixel(x, y);
                counts[color] = counts.TryGetValue(color, out var count) ? count + 1 : 1;
            }
        }

        return counts.OrderByDescending(entry => entry.Value).First().Key;
    }

    private void DrawCoast(int tileId, Vector2 position, int frame)
    {
        var id = CoastFrame(tileId, frame);
        DrawTextureRectRegion(
            _coastSheet,
            new Rect2(position, SubTile, SubTile),
            new Rect2(id % CoastColumns * SubTile, id / CoastColumns * SubTile, SubTile, SubTile));
    }

    private void DrawParoiTile(ParoiTile tile)
    {
        var source = TileSource(tile.TileId);
        var width = tile.To - tile.From;
        DrawTextureRectRegion(
            _tileset,
            new Rect2(tile.Position.X + tile.From, tile.Position.Y, width, SubTile),
            new Rect2(source.Position.X + tile.From, source.Position.Y, width, SubTile));
    }

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
        var wooden = _game?.Level.IsDoor(grille.Position) ?? true;
        if (IsThinDoor(grille.Position))
        {
            var height = foreground ? SubTile : Tile + SubTile;
            var column = wooden ? ThinDoorColumn : ThinGrilleColumn;
            DrawTextureRectRegion(_doors, new Rect2(x, y - SubTile, Tile, height), new Rect2(column, frameTop, Tile, height));
            return;
        }

        if (!foreground)
        {
            var column = wooden ? ThickDoorColumn : ThickGrilleColumn;
            DrawTextureRectRegion(_doors, new Rect2(x, y, Tile, Tile), new Rect2(column, ThickDoorTop + frameTop, Tile, Tile));
        }
    }

    private void DrawArch(Cell cell, bool foreground)
    {
        var frameTop = (GrilleFrames - 1) * DoorFrameStride;
        var x = cell.X * Tile;
        var y = cell.Y * Tile;
        if (IsThinDoor(cell))
        {
            var height = foreground ? SubTile : Tile + SubTile;
            DrawTextureRectRegion(_doors, new Rect2(x, y - SubTile, Tile, height), new Rect2(ThinGrilleColumn, frameTop, Tile, height));
            return;
        }

        if (!foreground)
        {
            DrawTextureRectRegion(_doors, new Rect2(x, y, Tile, Tile), new Rect2(ThickGrilleColumn, ThickDoorTop + frameTop, Tile, Tile));
        }
    }

    private void DrawLever(Cell lever)
    {
        if (_game is null)
        {
            return;
        }

        var leverFrame = _game.IsLeverPulled(lever)
            ? GrilleFrames - 1
            : _game.Level.LeverTarget(lever) is { } target && _game.GrilleAt(target) is { } herse ? GrilleFrame(herse) : 0;
        DrawTextureRectRegion(
            _mechanisms,
            new Rect2(
                lever.X * Tile + (Tile - LeverRegion.Size.X) / 2f,
                AboveThinWall(lever)
                    ? lever.Y * Tile + SubTile - LeverRegion.Size.Y - 1
                    : lever.Y * Tile + Tile - LeverRegion.Size.Y - LeverBottomMargin,
                LeverRegion.Size.X,
                LeverRegion.Size.Y),
            new Rect2(LeverRegion.Position.X + leverFrame * LeverFrameStride, LeverRegion.Position.Y, LeverRegion.Size.X, LeverRegion.Size.Y));
    }

    private static double ImpaleDuration => SpikeAnimationDuration + BloodFrames * BloodFrameDuration + ImpaleHold;

    private void DrawBlood()
    {
        if (_impale is not { } impale)
        {
            return;
        }

        var elapsed = _time - impale.Start - SpikeAnimationDuration;
        if (elapsed < 0)
        {
            return;
        }

        var frame = Math.Min(BloodFrames - 1, (int)(elapsed / BloodFrameDuration));
        var center = CellCenter(impale.Cell);
        DrawTextureRectRegion(
            _blood,
            new Rect2(center.X - BloodFrameSize / 2f, center.Y - BloodFrameSize / 2f, BloodFrameSize, BloodFrameSize),
            new Rect2(frame % BloodColumns * BloodFrameSize, frame / BloodColumns * BloodFrameSize, BloodFrameSize, BloodFrameSize));
    }

    private bool AboveThinWall(Cell cell) => _parois.TryGetValue(new Cell(cell.X, cell.Y + 1), out var horizontal) && horizontal;

    private static bool DescendsEast(LevelDefinition level, Cell stairs) =>
        IsWalkGround(level.TerrainAt(new Cell(stairs.X + 1, stairs.Y))) && !IsWalkGround(level.TerrainAt(new Cell(stairs.X - 1, stairs.Y)));

    private int ChestFrame(Cell chest)
    {
        var open = _game?.IsChestOpen(chest) ?? false;
        var known = _chestStates.TryGetValue(chest, out var state);
        if (!known || state.Open != open)
        {
            state = (open, known ? _time : double.NegativeInfinity);
            _chestStates[chest] = state;
        }

        var progress = Math.Clamp((_time - state.ChangedAt) / ChestAnimationDuration, 0.0, 1.0);
        var openness = state.Open ? progress : 1.0 - progress;
        return (int)Math.Round(openness * (ChestFrames - 1));
    }

    private int SpikeFrame(Cell spikes)
    {
        var raised = _game?.SpikesRaised(spikes) ?? false;
        var known = _spikeStates.TryGetValue(spikes, out var state);
        if (!known || state.Raised != raised)
        {
            state = (raised, known ? _time : double.NegativeInfinity);
            _spikeStates[spikes] = state;
        }

        var progress = Math.Clamp((_time - state.ChangedAt) / SpikeAnimationDuration, 0.0, 1.0);
        var height = state.Raised ? progress : 1.0 - progress;
        return (int)Math.Round(height * (SpikeFrames - 1));
    }

    private static Rect2 AltarRect(Cell first, int width) =>
        new(
            first.X * Tile + (width * Tile - AltarRegion.Size.X) / 2f,
            first.Y * Tile + Tile - AltarRegion.Size.Y - ContainerBottomMargin,
            AltarRegion.Size.X,
            AltarRegion.Size.Y);

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
        _waterUnder.Clear();
        _waterOver.Clear();
        _reflectionArea.Clear();
        _levers.Clear();
        _stairsUp.Clear();
        _arches.Clear();
        _shading.Clear();
        _thinDoors.Clear();
        FindParois(level);

        var width = level.Width * 2;
        var height = level.Height * 2;

        Terrain TerrainOf(int sx, int sy) =>
            sx >= 0 && sy >= 0 && sx < width && sy < height ? ShownTerrain(level, new Cell(sx / 2, sy / 2)) : Terrain.Void;
        bool Floor(int sx, int sy) => IsWalkGround(TerrainOf(sx, sy));
        bool Water(int sx, int sy) => TerrainOf(sx, sy) == Terrain.Water;
        bool Dry(int sx, int sy) => Floor(sx, sy) && !Water(sx, sy);
        bool Door(int sx, int sy) => TerrainOf(sx, sy) is Terrain.Grille or Terrain.Arch;
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

        bool ThinDoor(int sx, int sy) =>
            Door(sx, sy) && (Floor(sx, sy - (sy % 2 + 2) % 2 - 1) || ThinRun(sx, sy - (sy % 2 + 2) % 2 + 1));
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

        int? BankTile(int sx, int sy)
        {
            var north = Water(sx, sy - 1);
            var south = Water(sx, sy + 1);
            var east = Water(sx + 1, sy);
            var west = Water(sx - 1, sy);
            return north && west ? BankNorthWestTile
                : north && east ? BankNorthEastTile
                : north ? BankNorthTile
                : south && west ? BankSouthWestTile
                : south && east ? BankSouthEastTile
                : south ? BankSouthTile
                : west ? Band(sx - 1, sy) ? BankWestBandTile : BankWestTile
                : east ? Band(sx + 1, sy) ? BankEastBandTile : BankEastTile
                : Water(sx + 1, sy - 1) ? BankCornerNorthEastTile
                : Water(sx - 1, sy - 1) ? BankCornerNorthWestTile
                : Water(sx + 1, sy + 1) ? BankCornerSouthEastTile
                : Water(sx - 1, sy + 1) ? BankCornerSouthWestTile
                : null;
        }

        bool Band(int sx, int sy) => Water(sx, sy) && (Dry(sx, sy - 1) || FaceBottom(sx, sy - 1));

        void AddWater(int sx, int sy, Vector2 position)
        {
            _waterUnder.Add((WaterTile, NoShade, position));
            if (Band(sx, sy))
            {
                _waterUnder.Add((!Band(sx - 1, sy) ? BandWestTile : !Band(sx + 1, sy) ? BandEastTile : BandTile, NoShade, position));
                _reflectionArea.Add((sx, sy));
                var foot = FaceBottom(sx, sy - 1)
                    ? !FaceBottom(sx - 1, sy - 1) ? FaceFootWestTile : !FaceBottom(sx + 1, sy - 1) ? FaceFootEastTile : FaceFootTile
                    : !Dry(sx - 1, sy - 1) && Water(sx - 1, sy) ? SlabWestTile
                    : !Dry(sx + 1, sy - 1) && Water(sx + 1, sy) ? SlabEastTile
                    : SlabTile;
                _waterOver.Add((foot, position));
                return;
            }

            if (!Floor(sx, sy + 1) && !FaceTop(sx, sy + 1))
            {
                _waterOver.Add((FoamTile, position));
                return;
            }

            _reflectionArea.Add((sx, sy));
        }

        void AddGround(int sx, int sy, Vector2 position)
        {
            if (Water(sx, sy))
            {
                AddWater(sx, sy, position);
                return;
            }

            var bank = Dry(sx, sy) ? BankTile(sx, sy) : null;
            var shaded = Shaded(sx, sy);
            var shadeTile = shaded ? ShadeTile(sx, sy) : NoShade;
            if (shaded)
            {
                _shading[(sx, sy)] = shadeTile;
            }

            if (bank is { } bankTile)
            {
                _waterUnder.Add((bankTile, shadeTile, position));
                return;
            }

            _terrainTiles.Add((FloorTile, position));
            if (shaded)
            {
                _terrainTiles.Add((shadeTile, position));
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
                        if (Water(sx, sy + 1))
                        {
                            var waterFace = Floor(sx - 1, sy) ? WaterFaceWestTile : Floor(sx + 1, sy) ? WaterFaceEastTile : WaterFaceTile;
                            _waterUnder.Add((waterFace, NoShade, position));
                            continue;
                        }
                    }

                    var first = bottom ? FaceBottomTile : FaceTopTile;
                    var tileId = Floor(sx - 1, sy) ? first
                        : Floor(sx + 1, sy) ? first + 2
                        : bottom && ThinRun(sx, sy) ? ThinFaceBottomTile
                        : first + 1;
                    _terrainTiles.Add((tileId, position));
                    continue;
                }

                if (Water(sx + 1, sy))
                {
                    _waterUnder.Add((Band(sx + 1, sy) ? WallWaterEastBandTile : WallWaterEastTile, NoShade, position));
                    continue;
                }

                if (Water(sx - 1, sy))
                {
                    _waterUnder.Add((Band(sx - 1, sy) ? WallWaterWestBandTile : WallWaterWestTile, NoShade, position));
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

                if (level.TerrainAt(cell) == Terrain.Lever)
                {
                    _levers.Add(cell);
                }

                if (level.TerrainAt(cell) == Terrain.StairsUp)
                {
                    _stairsUp.Add(cell);
                }

                if (level.TerrainAt(cell) == Terrain.Arch)
                {
                    _arches.Add(cell);
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
        _alcoves.Clear();
        _containerLooks.Clear();
        _breaks.Clear();

        bool IsFace(Cell cell) =>
            IsMass(level, cell) && level.TerrainAt(cell) == Terrain.Wall && IsOpenGround(ShownTerrain(level, new Cell(cell.X, cell.Y + 1)));

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
        _alcoves.AddRange(level.Alcoves);
        foreach (var layer in _reflections)
        {
            BuildReflections(layer, random);
        }

        foreach (var container in _game?.Containers ?? Array.Empty<Cell>())
        {
            var looks = level.TerrainAt(container) switch
            {
                Terrain.Barrel => BarrelRegions,
                Terrain.Vase => VaseRegions,
                _ => CrateRegions,
            };
            _containerLooks[container] = looks[random.Next(looks.Length)];
        }

        foreach (var face in faces)
        {
            var left = new Cell(face.X - 1, face.Y);
            var right = new Cell(face.X + 1, face.Y);
            if (level.TerrainAt(left) == Terrain.Wall && IsMass(level, left) && !IsFace(left) && random.NextDouble() < WebChance)
            {
                _webs.Add((new Vector2(face.X * Tile, face.Y * Tile + WebOffsetY), false));
            }

            if (level.TerrainAt(right) == Terrain.Wall && IsMass(level, right) && !IsFace(right) && random.NextDouble() < WebChance)
            {
                _webs.Add((new Vector2(face.X * Tile + Tile - WebSize, face.Y * Tile + WebOffsetY), true));
            }
        }

        _coins.Clear();
        _altars.Clear();
        for (var y = 0; y < level.Height; y++)
        {
            for (var x = 0; x < level.Width; x++)
            {
                var cell = new Cell(x, y);
                if (level.TerrainAt(cell) != Terrain.Altar || level.TerrainAt(new Cell(x - 1, y)) == Terrain.Altar)
                {
                    continue;
                }

                var width = 1;
                while (level.TerrainAt(new Cell(x + width, y)) == Terrain.Altar)
                {
                    width++;
                }

                _altars.Add((cell, width));
            }
        }

        foreach (var item in _decor)
        {
            var origin = new Vector2(item.Position.X * Tile, item.Position.Y * Tile);
            if (item.Id == ElementIds.Coins)
            {
                var coin = CoinRegions[random.Next(CoinRegions.Length)];
                _coins.Add((coin, new Vector2(
                    Mathf.Round(origin.X + (Tile - coin.Size.X) / 2f + random.Next(-4, 5)),
                    Mathf.Round(origin.Y + Tile - coin.Size.Y - 4 + random.Next(-3, 2)))));
                continue;
            }

            if (HangingRegions.TryGetValue(item.Id, out var hanging))
            {
                _banners.Add((hanging, new Vector2(origin.X, origin.Y + BannerOffsetY)));
            }
            else if (item.Id == ElementIds.Shield)
            {
                _shields.Add((ShieldRegion, new Rect2(origin.X + SubTile / 2f, origin.Y + ShieldOffsetY, SubTile, SubTile)));
            }
        }

        BuildParoiTiles(level);
    }

    private void FindParois(LevelDefinition level)
    {
        _parois.Clear();
        for (var y = 0; y < level.Height; y++)
        {
            for (var x = 0; x < level.Width; x++)
            {
                var cell = new Cell(x, y);
                if (level.TerrainAt(cell) != Terrain.Wall || IsMassWall(level, cell))
                {
                    continue;
                }

                _parois[cell] = IsWallish(level, new Cell(x - 1, y)) || IsWallish(level, new Cell(x + 1, y));
            }
        }
    }

    private static bool IsWallish(LevelDefinition level, Cell cell) =>
        level.InBounds(cell) && level.TerrainAt(cell) is Terrain.Wall or Terrain.Grille or Terrain.Arch;

    private static bool IsMassWall(LevelDefinition level, Cell cell)
    {
        if (cell.X == 0 || cell.Y == 0 || cell.X == level.Width - 1 || cell.Y == level.Height - 1)
        {
            return true;
        }

        for (var dy = -1; dy <= 0; dy++)
        {
            for (var dx = -1; dx <= 0; dx++)
            {
                if (IsWallish(level, new Cell(cell.X + dx, cell.Y + dy))
                    && IsWallish(level, new Cell(cell.X + dx + 1, cell.Y + dy))
                    && IsWallish(level, new Cell(cell.X + dx, cell.Y + dy + 1))
                    && IsWallish(level, new Cell(cell.X + dx + 1, cell.Y + dy + 1)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private Terrain ShownTerrain(LevelDefinition level, Cell cell) =>
        _parois.ContainsKey(cell) ? Terrain.Floor : level.TerrainAt(cell);

    private bool IsMass(LevelDefinition level, Cell cell) =>
        IsWallish(level, cell) && !_parois.ContainsKey(cell);

    private void BuildReflections(List<(int TileId, Vector2 Position)> layer, Random random)
    {
        layer.Clear();
        if (_reflectionArea.Count == 0)
        {
            return;
        }

        var area = _reflectionArea.OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToList();
        var used = new HashSet<(int X, int Y)>();
        var goal = ReflectionCoverage * area.Count;
        var placed = new List<((int X, int Y) Cell, int TileId)>();
        for (var attempt = 0; attempt < ReflectionAttempts && used.Count < goal; attempt++)
        {
            var origin = area[random.Next(area.Count)];
            var stamp = ReflectionStamps[random.Next(ReflectionStamps.Length)];
            var width = 0;
            for (var index = 0; index < stamp.Length; index += 3)
            {
                width = Math.Max(width, stamp[index] + 1);
            }

            var shift = random.Next(width);
            placed.Clear();
            var tiles = stamp.Length / 3;
            var overlap = false;
            for (var index = 0; index < stamp.Length; index += 3)
            {
                var cell = (X: origin.X + stamp[index] - shift, Y: origin.Y + stamp[index + 1]);
                if (used.Contains(cell))
                {
                    overlap = true;
                    break;
                }

                if (_reflectionArea.Contains(cell))
                {
                    placed.Add((cell, stamp[index + 2]));
                }
            }

            if (overlap || placed.Count * 2 < tiles)
            {
                continue;
            }

            foreach (var (cell, tileId) in placed)
            {
                used.Add(cell);
                layer.Add((tileId, new Vector2(cell.X * SubTile, cell.Y * SubTile)));
            }
        }
    }

    private readonly record struct ParoiTile(int TileId, Vector2 Position, int From, int To, bool Front);

    private void BuildParoiTiles(LevelDefinition level)
    {
        _paroiTiles.Clear();
        _paroiFront.Clear();
        _frontTorches.Clear();

        bool IsParoi(Cell cell) => _parois.ContainsKey(cell);
        bool IsMassWall(Cell cell) => IsMass(level, cell) && level.TerrainAt(cell) == Terrain.Wall;
        bool Horizontal(Cell cell) => _parois.TryGetValue(cell, out var horizontal) && horizontal;
        bool EastEnd(Cell cell) => Horizontal(cell) && !IsWallish(level, new Cell(cell.X + 1, cell.Y));
        bool WestEnd(Cell cell) => Horizontal(cell) && !IsWallish(level, new Cell(cell.X - 1, cell.Y));
        Cell Up(Cell cell) => new(cell.X, cell.Y - 1);
        Cell Down(Cell cell) => new(cell.X, cell.Y + 1);
        bool IsPillar(Cell cell) =>
            IsParoi(cell)
            && (!Horizontal(cell) || IsParoi(Up(cell)) || IsMassWall(Up(cell)) || IsParoi(Down(cell)) || IsMassWall(Down(cell)));

        var offsets = new Dictionary<Cell, int>();
        foreach (var start in _parois.Keys.Where(IsPillar).OrderBy(cell => cell.X).ThenBy(cell => cell.Y))
        {
            if (offsets.ContainsKey(start))
            {
                continue;
            }

            var run = new List<Cell> { start };
            while (IsPillar(Down(run[^1])))
            {
                run.Add(Down(run[^1]));
            }

            var offset = PillarCenterOffset;
            foreach (var cell in run)
            {
                if (EastEnd(cell))
                {
                    offset = PillarEastOffset;
                    break;
                }

                if (WestEnd(cell))
                {
                    offset = PillarWestOffset;
                    break;
                }
            }

            foreach (var cell in run)
            {
                offsets[cell] = offset;
            }
        }

        var shafts = new List<ParoiTile>();
        var blocks = new List<ParoiTile>();
        var overlays = new List<ParoiTile>();
        ParoiTile Full(int tileId, float x, float y, bool front = false) => new(tileId, new Vector2(x, y), 0, SubTile, front);
        ParoiTile Pillar(int tileId, float x, float y, bool front = false) => new(tileId, new Vector2(x, y), PillarFrom, PillarTo, front);

        foreach (var cell in _parois.Keys.OrderBy(cell => cell.Y).ThenBy(cell => cell.X))
        {
            var x = cell.X * Tile;
            var y = cell.Y * Tile;
            var up = IsParoi(Up(cell));
            var upMass = IsMassWall(Up(cell));
            var down = IsParoi(Down(cell));
            var downMass = IsMassWall(Down(cell));
            var pillar = offsets.TryGetValue(cell, out var pillarOffset);
            var px = x + (pillar ? pillarOffset : PillarCenterOffset) - SubTile / 2f;

            if (Horizontal(cell))
            {
                var linkedUp = up || upMass;
                var cornerWest = pillar && pillarOffset == PillarWestOffset && WestEnd(cell) && linkedUp;
                var cornerEast = pillar && pillarOffset == PillarEastOffset && EastEnd(cell) && linkedUp;
                blocks.Add(Full(cornerWest ? CornerWestTopTile : BlockTopLeftTile, x, y - SubTile, true));
                blocks.Add(Full(cornerEast ? CornerEastTopTile : BlockTopRightTile, x + SubTile, y - SubTile, true));
                blocks.Add(Full(cornerWest ? CornerWestMiddleTile : BlockMiddleLeftTile, x, y));
                blocks.Add(Full(cornerEast ? CornerEastMiddleTile : BlockMiddleRightTile, x + SubTile, y));
                blocks.Add(Full(cornerWest ? CornerWestBottomTile : BlockBottomLeftTile, x, y + SubTile));
                blocks.Add(Full(cornerEast ? CornerEastBottomTile : BlockBottomRightTile, x + SubTile, y + SubTile));
                if (WestEnd(cell) && !cornerWest)
                {
                    blocks.Add(Full(BlockWestTopTile, x - SubTile, y - SubTile, true));
                    blocks.Add(Full(BlockWestMiddleTile, x - SubTile, y));
                    blocks.Add(Full(BlockWestBottomTile, x - SubTile, y + SubTile));
                }

                if (EastEnd(cell) && !cornerEast)
                {
                    blocks.Add(Full(BlockEastTopTile, x + Tile, y - SubTile, true));
                    blocks.Add(Full(BlockEastMiddleTile, x + Tile, y));
                    blocks.Add(Full(BlockEastBottomTile, x + Tile, y + SubTile));
                }

                if (level.Torches.Contains(cell))
                {
                    _frontTorches.Add(cell);
                }

                if (!pillar)
                {
                    continue;
                }

                var corner = cornerWest || cornerEast;
                if (upMass)
                {
                    shafts.Add(Pillar(PilasterLedgeTile, px, y - 3 * SubTile));
                    shafts.Add(Pillar(PilasterTopTile, px, y - Tile));
                }

                if (linkedUp && !corner)
                {
                    overlays.Add(new ParoiTile(CrossTopTile, new Vector2(px, y - SubTile), NotchFrom, NotchTo, true));
                    overlays.Add(Pillar(CrossMiddleTile, px, y));
                }

                if (down || downMass)
                {
                    if (!linkedUp || !corner)
                    {
                        overlays.Add(Pillar(CrossMiddleTile, px, y));
                    }

                    var end = corner || WestEnd(cell) || EastEnd(cell);
                    overlays.Add(new ParoiTile(
                        CrossBottomTile,
                        new Vector2(px, y + SubTile),
                        end ? PillarFrom : NotchFrom,
                        end ? PillarTo : NotchTo,
                        false));
                }

                continue;
            }

            if (upMass)
            {
                shafts.Add(Pillar(PilasterLedgeTile, px, y - 3 * SubTile));
                shafts.Add(Pillar(PilasterTopTile, px, y - Tile));
                shafts.Add(Pillar(PilasterBottomTile, px, y - SubTile));
            }
            else if (!up)
            {
                shafts.Add(Full(PillarCapTile, px, y - SubTile));
            }

            shafts.Add(Full(PillarUpperTile, px, y));
            if (down || downMass)
            {
                shafts.Add(Full(PillarLowerTile, px, y + SubTile));
            }
            else
            {
                shafts.Add(Full(PillarEndTile, px, y + SubTile));
                shafts.Add(Full(PillarBaseTile, px, y + Tile));
            }
        }

        foreach (var tile in shafts.Concat(blocks).Concat(overlays))
        {
            _paroiTiles.Add(tile);
            if (tile.Front)
            {
                _paroiFront.Add(tile);
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
