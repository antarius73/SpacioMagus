using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace SpacioMagus;

public partial class TitleScene : SpellScene
{
    private const int SubTile = 16;
    private const float Zoom = 2f;
    private const int GreenRow = 5;
    private const int RedRow = 6;
    private const int BatRow = 9;
    private const int BatHeight = 26;
    private const int BatOffsetY = -10;
    private const int BatFrames = 3;
    private const int BlobFrames = 4;
    private const int FireFrames = 6;
    private const int FireColumn = 39;
    private const int FireHeight = 48;
    private const int FireOffsetY = -14;
    private const int GlowSize = 80;
    private const int GlowOffsetX = -24;
    private const int GlowOffsetY = -26;
    private const int PotionFrames = 4;
    private const int VignetteScale = 2;
    private const double MagusIdleDuration = 1.4;
    private const double SwapIdleDuration = 0.6;
    private const double SwapSelectionDuration = 0.5;
    private const double SwapStunnedDuration = 1.6;
    private const double SwapPhase = 1.1;
    private const int SwapStunTurns = 4;
    private const int RedStunTurns = 3;
    private const float VignetteInner = 125f;
    private const float VignetteRange = 110f;
    private const float VignetteStrength = 0.85f;
    private const float TopShadeHeight = 70f;
    private const float TopShadeStrength = 0.9f;

    private static readonly string[] Map =
    {
        "WWWWW.WWWWWW",
        "WWWWW.WWW.WW",
        "WWWWW.WWW.WW",
        "WWWW...W...W",
        "W..........W",
        "WWWW...WWW.W",
        "WWWWW.WWWW.W",
        "WWWWW.WWWW.W",
        "WWWWW.....WW",
        "WWWWW.WWWWWW",
        "WWWWWWWWWWWW",
    };

    private static readonly Vector2 WorldFocus = new(168, 144);
    private const float ScreenFocusY = 200f;
    private static readonly Color Background = LevelView.BackgroundColor;
    private static readonly Color Shade = new(18f / 255f, 16f / 255f, 30f / 255f);
    private static readonly Color StunTint = new(0.6f, 0.6f, 0.75f);
    private static readonly Color GlowColor = new(1f, 0.67f, 0.31f);

    private static readonly Vector2I MagusFirst = new(5, 4);
    private static readonly Vector2I MagusSecond = new(4, 4);
    private static readonly Vector2I SwapBlobCell = new(3, 4);
    private static readonly Vector2I SwapBatCell = new(5, 3);
    private static readonly Vector2I IdleBatCell = new(5, 2);
    private static readonly Vector2I IdleBlobCell = new(6, 5);
    private static readonly Vector2I StunnedBlobCell = new(5, 6);
    private static readonly Vector2I PotionCell = new(6, 4);
    private static readonly Vector2I[] Torches = { new(4, 2), new(3, 3) };

    private static readonly (string Texture, Rect2 Source, Vector2 Position)[] Props =
    {
        ("objets", new Rect2(16, 80, 32, 32), new Vector2(128, 160)),
        ("objets", new Rect2(144, 112, 16, 16), new Vector2(200, 78)),
        ("objets_kao", new Rect2(0, 32, 32, 32), new Vector2(224, 96)),
        ("objets_kao", new Rect2(320, 0, 32, 32), new Vector2(96, 96)),
        ("objets_kao", new Rect2(352, 0, 32, 32), new Vector2(192, 64)),
        ("objets", new Rect2(56, 80, 16, 32), new Vector2(130, 94)),
        ("objets", new Rect2(112, 80, 16, 32), new Vector2(144, 100)),
        ("objets", new Rect2(192, 80, 16, 32), new Vector2(228, 124)),
        ("objets", new Rect2(272, 16, 32, 48), new Vector2(320, 144)),
        ("objets_kao", new Rect2(160, 32, 32, 32), new Vector2(32, 128)),
        ("objets", new Rect2(80, 80, 16, 32), new Vector2(296, 256)),
        ("objets_kao", new Rect2(128, 32, 32, 32), new Vector2(160, 256)),
    };

    private static readonly (int Tile, Vector2 Position)[] Cracks =
    {
        (228, new Vector2(150, 140)),
        (245, new Vector2(230, 135)),
        (229, new Vector2(170, 290)),
        (246, new Vector2(335, 200)),
    };

    private readonly List<(int Tile, Vector2 Position)> _tiles = new();
    private readonly Dictionary<string, Texture2D> _textures;
    private readonly Texture2D _fire = Load("res://assets/sprites/donjon_feu.png");
    private readonly Texture2D _potion = Load("res://assets/sprites/fiole_bleue.png");
    private Texture2D? _glow;
    private Texture2D? _vignette;
    private Vector2 _vignetteSize;
    private Transform2D _scene = Transform2D.Identity;

    public TitleScene()
    {
        _textures = new Dictionary<string, Texture2D>
        {
            ["objets"] = Load("res://assets/sprites/donjon_objets.png"),
            ["objets_kao"] = Load("res://assets/sprites/items.png"),
        };
        BuildTiles();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), Background);
        var origin = new Vector2(
            Mathf.Round(Size.X / 2f - WorldFocus.X * Zoom),
            Mathf.Round(ScreenFocusY - WorldFocus.Y * Zoom));
        _scene = new Transform2D(new Vector2(Zoom, 0), new Vector2(0, Zoom), origin);
        DrawSetTransformMatrix(_scene);

        foreach (var (tile, position) in _tiles)
        {
            DrawTile(tile, position.X, position.Y);
        }

        foreach (var (tile, position) in Cracks)
        {
            DrawTile(tile, position.X, position.Y);
        }

        foreach (var (texture, source, position) in Props)
        {
            DrawTextureRectRegion(_textures[texture], new Rect2(position, source.Size), source);
        }

        DrawTorches();
        var potionFrame = (int)(Time * 5) % PotionFrames;
        DrawTextureRectRegion(_potion, new Rect2(CellOrigin(PotionCell), Cell, Cell), new Rect2(potionFrame * Cell, 0, Cell, Cell));

        var magus = DrawMagusPhase(out var magusCell, out var magusElapsed);
        DrawActors(magusCell);
        DrawMagusLayer(magus, magusCell, magusElapsed);

        DrawSetTransformMatrix(Transform2D.Identity);
        if (_vignette is null || _vignetteSize != Size)
        {
            _vignette = BuildVignette();
            _vignetteSize = Size;
        }

        DrawTextureRect(_vignette, new Rect2(Vector2.Zero, Size), false);
    }

    private (bool Teleporting, float Alpha) DrawMagusPhase(out Vector2I cell, out double elapsed)
    {
        var leg = MagusIdleDuration + SpellFx.TeleportDuration;
        var t = Time % (2 * leg);
        var outward = t < leg;
        var local = outward ? t : t - leg;
        var from = outward ? MagusFirst : MagusSecond;
        var to = outward ? MagusSecond : MagusFirst;
        if (local < MagusIdleDuration)
        {
            cell = from;
            elapsed = 0;
            return (false, 1f);
        }

        elapsed = local - MagusIdleDuration;
        cell = SpellFx.IsDeparting(elapsed) ? from : to;
        var origin = CellOrigin(cell);
        SpellFx.DrawTeleportAura(this, SpellFx.AuraBack, origin, elapsed);
        return (true, SpellFx.TeleportMagusAlpha(elapsed));
    }

    private void DrawMagusLayer((bool Teleporting, float Alpha) magus, Vector2I cell, double elapsed)
    {
        var origin = CellOrigin(cell);
        DrawMagus(origin, magus.Alpha);
        if (magus.Teleporting)
        {
            SpellFx.DrawTeleportAura(this, SpellFx.AuraFront, origin, elapsed);
        }
    }

    private void DrawActors(Vector2I magusCell)
    {
        var leg = SwapIdleDuration + SwapSelectionDuration + SpellFx.SwapDuration + SwapStunnedDuration;
        var t = (Time + SwapPhase) % (2 * leg);
        var swapped = t >= leg;
        var local = swapped ? t - leg : t;
        var blobCell = swapped ? SwapBatCell : SwapBlobCell;
        var batCell = swapped ? SwapBlobCell : SwapBatCell;
        var swapStart = SwapIdleDuration + SwapSelectionDuration;

        var actors = new List<(Vector2I Cell, Action Draw)>
        {
            (IdleBatCell, () => DrawBat(IdleBatCell, magusCell, false, 1f, 0.0)),
            (IdleBlobCell, () => DrawBlob(IdleBlobCell, GreenRow, magusCell, false, 1f, 0.3)),
            (StunnedBlobCell, () => DrawBlob(StunnedBlobCell, RedRow, magusCell, true, 1f, 0.0)),
        };

        var shocks = new List<(Vector2I Cell, float Alpha)>();
        var stunned = false;
        if (local < swapStart)
        {
            actors.Add((blobCell, () => DrawBlob(blobCell, GreenRow, magusCell, false, 1f, 0.6)));
            actors.Add((batCell, () => DrawBat(batCell, magusCell, false, 1f, 0.5)));
            if (local >= SwapIdleDuration)
            {
                shocks.Add((blobCell, (float)Math.Clamp((local - SwapIdleDuration) / SpellFx.ShockFadeInDuration, 0.0, 1.0)));
            }
        }
        else if (local - swapStart < SpellFx.SwapDuration)
        {
            var elapsed = local - swapStart;
            var alpha = SpellFx.SwapMonsterAlpha(elapsed, out var atOrigin);
            var shownBlob = atOrigin ? blobCell : batCell;
            var shownBat = atOrigin ? batCell : blobCell;
            actors.Add((shownBlob, () => DrawBlob(shownBlob, GreenRow, magusCell, !atOrigin, alpha, 0.6)));
            actors.Add((shownBat, () => DrawBat(shownBat, magusCell, !atOrigin, alpha, 0.5)));
            var (first, second) = SpellFx.SwapShockAlphas(elapsed);
            shocks.Add((blobCell, first));
            shocks.Add((batCell, second));
        }
        else
        {
            stunned = true;
            actors.Add((batCell, () => DrawBlob(batCell, GreenRow, magusCell, true, 1f, 0.6)));
            actors.Add((blobCell, () => DrawBat(blobCell, magusCell, true, 1f, 0.5)));
        }

        foreach (var actor in actors.OrderBy(actor => actor.Cell.Y))
        {
            actor.Draw();
        }

        foreach (var (cell, alpha) in shocks)
        {
            SpellFx.DrawShock(this, CellOrigin(cell), alpha, Time);
        }

        SpellFx.DrawStun(this, CellOrigin(StunnedBlobCell), RedStunTurns, Time);
        if (stunned)
        {
            SpellFx.DrawStun(this, CellOrigin(blobCell), SwapStunTurns, Time);
            SpellFx.DrawStun(this, CellOrigin(batCell), SwapStunTurns, Time);
        }
    }

    private void DrawBlob(Vector2I cell, int row, Vector2I magusCell, bool stunned, float alpha, double phase)
    {
        if (alpha <= 0f)
        {
            return;
        }

        var frame = stunned ? 0 : (int)((Time + phase) * 5) % BlobFrames;
        var tint = stunned ? new Color(StunTint, alpha) : new Color(1f, 1f, 1f, alpha);
        DrawSprite(Animations, new Rect2(CellOrigin(cell), Cell, Cell), new Rect2(frame * Cell, row * Cell, Cell, Cell), cell.X < magusCell.X, tint);
    }

    private void DrawBat(Vector2I cell, Vector2I magusCell, bool stunned, float alpha, double phase)
    {
        if (alpha <= 0f)
        {
            return;
        }

        var frame = stunned ? 0 : (int)((Time + phase) * 8) % BatFrames;
        var bob = stunned ? 0f : Mathf.Round((float)Math.Sin((Time + phase) * 6.0) * 2f);
        var tint = stunned ? new Color(StunTint, alpha) : new Color(1f, 1f, 1f, alpha);
        var origin = CellOrigin(cell);
        var target = new Rect2(origin.X, origin.Y + BatOffsetY + bob, Cell, BatHeight);
        DrawSprite(Animations, target, new Rect2(frame * Cell, BatRow * Cell, Cell, BatHeight), cell.X < magusCell.X, tint);
    }

    private void DrawSprite(Texture2D texture, Rect2 target, Rect2 source, bool flip, Color tint)
    {
        if (!flip)
        {
            DrawTextureRectRegion(texture, target, source, tint);
            return;
        }

        var mirror = new Transform2D(new Vector2(-1, 0), new Vector2(0, 1), new Vector2(target.Position.X + target.Size.X, target.Position.Y));
        DrawSetTransformMatrix(_scene * mirror);
        DrawTextureRectRegion(texture, new Rect2(Vector2.Zero, target.Size), source, tint);
        DrawSetTransformMatrix(_scene);
    }

    private void DrawTorches()
    {
        _glow ??= BuildGlow();
        var frame = (int)(Time * 8) % FireFrames;
        foreach (var torch in Torches)
        {
            var origin = CellOrigin(torch);
            DrawTexture(_glow, new Vector2(origin.X + GlowOffsetX, origin.Y + GlowOffsetY));
            DrawTextureRectRegion(
                _fire,
                new Rect2(origin.X, origin.Y + FireOffsetY, Cell, FireHeight),
                new Rect2(FireColumn, frame * FireHeight, Cell, FireHeight));
        }
    }

    private static Vector2 CellOrigin(Vector2I cell) => new(cell.X * Cell, cell.Y * Cell);

    private static bool IsWall(int x, int y) =>
        y < 0 || y >= Map.Length || x < 0 || x >= Map[y].Length || Map[y][x] == 'W';

    private void BuildTiles()
    {
        for (var y = 0; y < Map.Length; y++)
        {
            for (var x = 0; x < Map[y].Length; x++)
            {
                var px = x * Cell;
                var py = y * Cell;
                if (!IsWall(x, y))
                {
                    AddQuad(px, py, 138, 138, 138, 138);
                    if (IsWall(x, y - 1))
                    {
                        Add(250, px, py);
                        Add(250, px + SubTile, py);
                    }

                    continue;
                }

                var westWall = IsWall(x - 1, y);
                var eastWall = IsWall(x + 1, y);
                if (!IsWall(x, y + 1))
                {
                    var west = westWall && !IsWall(x - 1, y + 1);
                    var east = eastWall && !IsWall(x + 1, y + 1);
                    AddQuad(px, py, west ? 70 : 69, east ? 70 : 71, west ? 87 : 86, east ? 87 : 88);
                    continue;
                }

                var aboveFace = !IsWall(x, y + 2);
                AddQuad(
                    px,
                    py,
                    westWall ? 36 : 35,
                    eastWall ? 36 : 37,
                    aboveFace ? (westWall ? 53 : 52) : (westWall ? 36 : 35),
                    aboveFace ? (eastWall ? 53 : 54) : (eastWall ? 36 : 37));
                if (!IsWall(x, y - 1))
                {
                    Add(westWall ? 19 : 18, px, py);
                    Add(eastWall ? 19 : 20, px + SubTile, py);
                }
            }
        }
    }

    private void AddQuad(int x, int y, int topLeft, int topRight, int bottomLeft, int bottomRight)
    {
        Add(topLeft, x, y);
        Add(topRight, x + SubTile, y);
        Add(bottomLeft, x, y + SubTile);
        Add(bottomRight, x + SubTile, y + SubTile);
    }

    private void Add(int tile, int x, int y) => _tiles.Add((tile, new Vector2(x, y)));

    private static Texture2D BuildGlow()
    {
        var image = Image.CreateEmpty(GlowSize, GlowSize, false, Image.Format.Rgba8);
        var radius = GlowSize / 2f;
        for (var y = 0; y < GlowSize; y++)
        {
            for (var x = 0; x < GlowSize; x++)
            {
                var distance = new Vector2(x - radius, y - radius).Length();
                var alpha = distance < radius ? (1f - distance / radius) * 0.27f : 0f;
                image.SetPixel(x, y, new Color(GlowColor, alpha));
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    private Texture2D BuildVignette()
    {
        var width = Math.Max(1, (int)(Size.X / VignetteScale));
        var height = Math.Max(1, (int)(Size.Y / VignetteScale));
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var screen = new Vector2(x * VignetteScale, y * VignetteScale);
                var distance = new Vector2(screen.X - Size.X / 2f, (screen.Y - ScreenFocusY) * 1.05f).Length();
                var alpha = Mathf.Clamp((distance - VignetteInner) / VignetteRange, 0f, 1f) * VignetteStrength;
                if (screen.Y < TopShadeHeight)
                {
                    alpha = Math.Max(alpha, (TopShadeHeight - screen.Y) / TopShadeHeight * TopShadeStrength);
                }

                image.SetPixel(x, y, new Color(Shade, alpha));
            }
        }

        return ImageTexture.CreateFromImage(image);
    }
}
