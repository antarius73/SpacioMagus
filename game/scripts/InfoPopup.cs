using System;
using Godot;

namespace SpacioMagus;

public enum InfoSubject
{
    Door,
    Potion,
    GreenBlob,
    Stairs,
    RedBlob,
    Torch,
    Brazier,
    Container,
}

public partial class InfoPopup : CanvasLayer
{
    private const int ScreenMargin = 16;
    private const int TextureMargin = 10;
    private const float ContentWidth = 224f;
    private const float IllustrationHeight = 128f;
    private const float HintOpacity = 0.6f;
    private static readonly Color Backdrop = new(0.04f, 0.03f, 0.07f, 0.6f);

    public event Action? Closed;

    public static InfoPopup Create(InfoSubject subject)
    {
        var (title, body, illustration) = subject switch
        {
            InfoSubject.Door => (Texts.DoorTitle, Texts.Door, (Control)new DoorIllustration()),
            InfoSubject.Potion => (Texts.PotionTitle, Texts.Potion, (Control)new PotionIllustration()),
            InfoSubject.Stairs => (Texts.StairsTitle, Texts.Stairs, (Control)new StairsIllustration()),
            InfoSubject.RedBlob => (Texts.RedBlobTitle, Texts.RedBlob, (Control)new BlobIllustration(BlobIllustration.RedRow)),
            InfoSubject.Torch => (Texts.TorchTitle, Texts.Torch, (Control)new TorchIllustration()),
            InfoSubject.Brazier => (Texts.BrazierTitle, Texts.Brazier, (Control)new BrazierIllustration()),
            InfoSubject.Container => (Texts.ContainerTitle, Texts.Container, (Control)new ContainerIllustration()),
            _ => (Texts.GreenBlobTitle, Texts.GreenBlob, (Control)new BlobIllustration(BlobIllustration.GreenRow)),
        };

        var popup = new InfoPopup { Layer = 9 };

        var root = new Control { Theme = GuiAssets.Theme };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        popup.AddChild(root);

        var backdrop = new ColorRect { Color = Backdrop, MouseFilter = Control.MouseFilterEnum.Stop };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        backdrop.GuiInput += input =>
        {
            if (input is InputEventMouseButton { Pressed: true })
            {
                popup.Close();
            }
        };
        root.AddChild(backdrop);

        var area = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        area.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
        {
            area.AddThemeConstantOverride(side, ScreenMargin);
        }

        root.AddChild(area);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        area.AddChild(center);

        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxTexture
        {
            Texture = GuiAssets.Parchment,
            TextureMarginLeft = TextureMargin,
            TextureMarginRight = TextureMargin,
            TextureMarginTop = TextureMargin,
            TextureMarginBottom = TextureMargin,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 12,
            ContentMarginBottom = 12,
        });
        center.AddChild(panel);

        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);

        illustration.CustomMinimumSize = new Vector2(ContentWidth, IllustrationHeight);
        box.AddChild(illustration);

        var titleLabel = new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Center };
        titleLabel.AddThemeFontSizeOverride("font_size", GuiAssets.TitleFontSize);
        titleLabel.AddThemeColorOverride("font_color", GuiAssets.TextOnParchment);
        box.AddChild(titleLabel);

        var bodyLabel = new Label
        {
            Text = body,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(ContentWidth, 0),
        };
        bodyLabel.AddThemeColorOverride("font_color", GuiAssets.TextOnParchment);
        box.AddChild(bodyLabel);

        var hint = new Label { Text = Texts.CloseHint, HorizontalAlignment = HorizontalAlignment.Center };
        hint.AddThemeColorOverride("font_color", new Color(GuiAssets.TextOnParchment, HintOpacity));
        box.AddChild(hint);

        return popup;
    }

    private void Close()
    {
        if (IsQueuedForDeletion())
        {
            return;
        }

        QueueFree();
        Closed?.Invoke();
    }
}

public partial class Illustration : Control
{
    private const int TilesetColumns = 17;
    private const int SubTile = 16;
    private static Texture2D? _tiles;

    public Illustration()
    {
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    protected double Time { get; private set; }

    protected static Texture2D Tiles => _tiles ??= Load("res://assets/sprites/donjon_murs_sol.png");

    protected void DrawTile(int tileId, float x, float y)
    {
        var source = new Rect2(tileId % TilesetColumns * SubTile, tileId / TilesetColumns * SubTile, SubTile, SubTile);
        DrawTextureRectRegion(Tiles, new Rect2(x, y, SubTile, SubTile), source);
    }

    public override void _Process(double delta)
    {
        Time += delta;
        QueueRedraw();
    }

    protected static Texture2D Load(string path) =>
        GD.Load<Texture2D>(path) ?? throw new InvalidOperationException($"Texture introuvable : {path}");

    protected void BeginScene(Vector2 sceneSize, float scale)
    {
        var origin = new Vector2(
            Mathf.Round((Size.X - sceneSize.X * scale) / 2f),
            Mathf.Round((Size.Y - sceneSize.Y * scale) / 2f));
        DrawSetTransform(origin, 0f, new Vector2(scale, scale));
    }
}

public partial class DoorIllustration : Illustration
{
    private const int SubTile = 16;
    private const double ClosedHold = 0.8;
    private const double SwingDuration = 0.35;
    private const double CountStep = 0.5;
    private const int CountFrom = 3;
    private const int DoorFrames = 5;
    private const int DoorStride = 48;
    private static readonly Vector2 SceneSize = new(96, 64);

    private readonly Texture2D _doors = Load("res://assets/sprites/donjon_portes.png");

    public override void _Draw()
    {
        BeginScene(SceneSize, 2f);
        for (var x = 0; x < 6; x++)
        {
            DrawTile(53, x * SubTile, 0);
            DrawTile(70, x * SubTile, SubTile);
            DrawTile(87, x * SubTile, 2 * SubTile);
            DrawTile(138, x * SubTile, 3 * SubTile);
        }

        var openFor = CountStep * CountFrom;
        var cycle = ClosedHold + SwingDuration + openFor + SwingDuration;
        var t = Time % cycle;
        var frame = 0;
        int? counter = null;
        if (t >= ClosedHold && t < ClosedHold + SwingDuration)
        {
            frame = Swing((t - ClosedHold) / SwingDuration);
        }
        else if (t >= ClosedHold + SwingDuration && t < ClosedHold + SwingDuration + openFor)
        {
            frame = DoorFrames - 1;
            counter = CountFrom - (int)((t - ClosedHold - SwingDuration) / CountStep);
        }
        else if (t >= ClosedHold + SwingDuration + openFor)
        {
            frame = DoorFrames - 1 - Swing((t - ClosedHold - SwingDuration - openFor) / SwingDuration);
        }

        DrawTextureRectRegion(_doors, new Rect2(32, 16, 32, 32), new Rect2(32, 16 + frame * DoorStride, 32, 32));
        if (counter is { } value)
        {
            GuiAssets.DrawNumber(this, value, new Vector2(48, 22));
        }

        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    private static int Swing(double progress) => Math.Clamp((int)(progress * DoorFrames), 0, DoorFrames - 1);
}

public partial class PotionIllustration : Illustration
{
    private const int GemCount = 4;
    private const int GemStep = 14;
    private const double EmptyHold = 0.6;
    private const double FillStep = 0.3;
    private const double FullHold = 1.2;
    private const double PotionFrameDuration = 0.2;
    private const int PotionFrames = 4;
    private static readonly Vector2 SceneSize = new(112, 48);

    private readonly Texture2D _potion = Load("res://assets/sprites/fiole_bleue.png");

    public override void _Draw()
    {
        BeginScene(SceneSize, 2f);
        var potionFrame = (int)(Time / PotionFrameDuration) % PotionFrames;
        DrawTextureRectRegion(_potion, new Rect2(4, 8, 32, 32), new Rect2(potionFrame * 32, 0, 32, 32));

        var cycle = EmptyHold + FillStep * GemCount + FullHold;
        var t = Time % cycle;
        var filled = Math.Clamp((int)((t - EmptyHold) / FillStep) + 1, 0, GemCount);
        if (t < EmptyHold)
        {
            filled = 0;
        }

        for (var index = 0; index < GemCount; index++)
        {
            var source = new Rect2(index < filled ? 0 : GuiAssets.GemSize, 0, GuiAssets.GemSize, GuiAssets.GemSize);
            DrawTextureRectRegion(GuiAssets.Gems, new Rect2(48 + index * GemStep, 17, GuiAssets.GemSize, GuiAssets.GemSize), source);
        }

        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}

public partial class BlobIllustration : Illustration
{
    public const int GreenRow = 5;
    public const int RedRow = 6;

    private const double FrameDuration = 0.2;
    private const int Frames = 4;
    private static readonly Vector2 SceneSize = new(32, 32);

    private readonly Texture2D _animations = Load("res://assets/sprites/animations.png");
    private readonly int _row;

    public BlobIllustration()
        : this(GreenRow)
    {
    }

    public BlobIllustration(int row)
    {
        _row = row;
    }

    public override void _Draw()
    {
        BeginScene(SceneSize, 3f);
        var frame = (int)(Time / FrameDuration) % Frames;
        DrawTextureRectRegion(_animations, new Rect2(0, 0, 32, 32), new Rect2(frame * 32, _row * 32, 32, 32));
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}

public partial class TorchIllustration : Illustration
{
    private const int Cell = 32;
    private const int FireColumn = 39;
    private const int FireHeight = 48;
    private const int FireFrames = 6;
    private const int FireOffsetY = -8;
    private static readonly Vector2 SceneSize = new(112, 64);
    private static readonly Color FearColor = new(1f, 0.55f, 0.2f, 0.25f);

    private readonly Texture2D _fire = Load("res://assets/sprites/donjon_feu.png");

    public override void _Draw()
    {
        BeginScene(SceneSize, 2f);
        for (var x = 0; x < SceneSize.X; x += 16)
        {
            DrawTile(70, x, 0);
            DrawTile(87, x, 16);
            DrawTile(138, x, 32);
            DrawTile(138, x, 48);
        }

        DrawRect(new Rect2(8, Cell, 3 * Cell, Cell), FearColor);
        var frame = (int)(Time * 8) % FireFrames;
        DrawTextureRectRegion(_fire, new Rect2(40, FireOffsetY, Cell, FireHeight), new Rect2(FireColumn, frame * FireHeight, Cell, FireHeight));
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}

public partial class ContainerIllustration : Illustration
{
    private const int Cell = 32;
    private const double IdleDuration = 0.9;
    private const double BlinkDuration = 0.25;
    private const int Blinks = 3;
    private const double PotionDelay = 0.3;
    private const double BounceDuration = 0.35;
    private const double HoldDuration = 1.0;
    private const float BounceHeight = 8f;
    private static readonly Vector2 SceneSize = new(112, 64);
    private static readonly Rect2 Barrel = new(112, 80, 16, 24);
    private static readonly Rect2 Crate = new(96, 80, 16, 24);
    private static readonly Vector2 BarrelCell = new(24, 16);
    private static readonly Vector2 CrateCell = new(56, 16);

    private readonly Texture2D _objects = Load("res://assets/sprites/donjon_objets.png");
    private readonly Texture2D _potion = Load("res://assets/sprites/fiole_bleue.png");

    public override void _Draw()
    {
        BeginScene(SceneSize, 2f);
        for (var y = 0; y < SceneSize.Y; y += 16)
        {
            for (var x = 0; x < SceneSize.X; x += 16)
            {
                DrawTile(138, x, y);
            }
        }

        DrawObject(Crate, CrateCell);
        var cycle = IdleDuration + BlinkDuration + SpellFx.SakuraDuration + HoldDuration;
        var t = Time % cycle;
        var broken = t - IdleDuration;
        if (broken < 0 || (broken < BlinkDuration && (int)(broken / BlinkDuration * Blinks * 2) % 2 == 0))
        {
            DrawObject(Barrel, BarrelCell);
        }

        if (broken >= PotionDelay)
        {
            var since = broken - PotionDelay;
            var offset = 0f;
            if (since < BounceDuration)
            {
                var progress = (float)(since / BounceDuration);
                offset = -Mathf.Round(Mathf.Abs(Mathf.Cos(progress * Mathf.Pi * 2.5f)) * BounceHeight * (1f - progress));
            }

            var frame = (int)(Time * 5) % 4;
            DrawTextureRectRegion(_potion, new Rect2(BarrelCell.X, BarrelCell.Y + offset, Cell, Cell), new Rect2(frame * Cell, 0, Cell, Cell));
        }

        if (broken >= BlinkDuration)
        {
            SpellFx.DrawSakura(this, BarrelCell, broken - BlinkDuration);
        }

        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    private void DrawObject(Rect2 source, Vector2 cell) =>
        DrawTextureRectRegion(_objects, new Rect2(cell.X + (Cell - source.Size.X) / 2f, cell.Y + Cell - source.Size.Y - 2, source.Size.X, source.Size.Y), source);
}

public partial class BrazierIllustration : Illustration
{
    private const int Cell = 32;
    private const int BrazierColumn = 128;
    private const int BrazierSize = 48;
    private const int FireFrames = 6;
    private static readonly Vector2 SceneSize = new(112, 64);
    private static readonly Color FearColor = new(1f, 0.55f, 0.2f, 0.25f);

    private readonly Texture2D _fire = Load("res://assets/sprites/donjon_feu.png");

    public override void _Draw()
    {
        BeginScene(SceneSize, 2f);
        for (var y = 0; y < SceneSize.Y; y += 16)
        {
            for (var x = 0; x < SceneSize.X; x += 16)
            {
                DrawTile(138, x, y);
            }
        }

        DrawRect(new Rect2(8, 0, 3 * Cell, SceneSize.Y), FearColor);
        var frame = (int)(Time * 8) % FireFrames;
        DrawTextureRectRegion(_fire, new Rect2(32, 6, BrazierSize, BrazierSize), new Rect2(BrazierColumn, frame * BrazierSize, BrazierSize, BrazierSize));
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}

public partial class StairsIllustration : Illustration
{
    private const int Cell = 32;
    private const int IdleRow = 8;
    private const int WalkEastRow = 2;
    private const double WaitDuration = 0.8;
    private const double WalkDuration = 0.6;
    private const double FadeDuration = 0.4;
    private const double EmptyDuration = 0.6;
    private static readonly Vector2 SceneSize = new(112, 64);
    private static readonly Vector2 StartCell = new(24, 26);
    private static readonly Vector2 StairsCell = new(56, 26);
    private static readonly Rect2 StairsRegion = new(16, 80, 32, 32);

    private readonly Texture2D _objects = Load("res://assets/sprites/donjon_objets.png");
    private readonly Texture2D _magus = Load("res://assets/sprites/magus_pixel.png");

    public override void _Draw()
    {
        BeginScene(SceneSize, 2f);
        for (var x = 0; x < SceneSize.X; x += 16)
        {
            DrawTile(70, x, 0);
            DrawTile(87, x, 16);
            DrawTile(138, x, 32);
            DrawTile(138, x, 48);
        }

        DrawTextureRectRegion(_objects, new Rect2(StairsCell, Cell, Cell), StairsRegion);

        var cycle = WaitDuration + WalkDuration + FadeDuration + EmptyDuration;
        var t = Time % cycle;
        var position = StartCell;
        var alpha = 1f;
        var row = IdleRow;
        var frame = (int)(Time * 6) % 6;
        if (t >= WaitDuration && t < WaitDuration + WalkDuration)
        {
            var progress = (float)((t - WaitDuration) / WalkDuration);
            position = new Vector2(Mathf.Round(Mathf.Lerp(StartCell.X, StairsCell.X, progress)), StartCell.Y);
            row = WalkEastRow;
            frame = (int)(Time * 12) % 6;
        }
        else if (t >= WaitDuration + WalkDuration)
        {
            position = StairsCell;
            alpha = 1f - (float)Math.Clamp((t - WaitDuration - WalkDuration) / FadeDuration, 0.0, 1.0);
        }

        if (alpha > 0f)
        {
            DrawTextureRectRegion(_magus, new Rect2(position, Cell, Cell), new Rect2(frame * Cell, row * Cell, Cell, Cell), new Color(1f, 1f, 1f, alpha));
        }

        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}

public partial class GestureIllustration : Illustration
{
    private const double WaitDuration = 0.6;
    private const double CardDuration = 1.4;
    private const float RingRadius = 20f;
    private const float RingWidth = 3f;
    private const int RingPoints = 32;
    private static readonly Vector2 SceneSize = new(112, 64);
    private static readonly Color RingColor = new(0.95f, 0.89f, 0.74f);
    private static readonly Color CardColor = new(0.91f, 0.84f, 0.64f);
    private static readonly Color CardBorder = new(0.36f, 0.24f, 0.15f);

    private readonly Texture2D _doors = Load("res://assets/sprites/donjon_portes.png");

    public override void _Draw()
    {
        BeginScene(SceneSize, 2f);
        for (var x = 0; x < SceneSize.X; x += 16)
        {
            DrawTile(53, x, 0);
            DrawTile(70, x, 16);
            DrawTile(87, x, 32);
            DrawTile(138, x, 48);
        }

        DrawTextureRectRegion(_doors, new Rect2(16, 16, 32, 32), new Rect2(32, 16, 32, 32));

        var hold = LevelView.LongPressDuration;
        var cycle = WaitDuration + hold + CardDuration;
        var t = Time % cycle;
        var center = new Vector2(32, 32);
        if (t >= WaitDuration && t < WaitDuration + hold)
        {
            var progress = (float)((t - WaitDuration) / hold);
            DrawArc(center, RingRadius / 2f, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau * progress, RingPoints, RingColor, RingWidth / 2f);
        }
        else if (t >= WaitDuration + hold)
        {
            var card = new Rect2(60, 12, 46, 40);
            DrawRect(card, CardColor);
            DrawRect(card, CardBorder, false, 1f);
            for (var line = 0; line < 4; line++)
            {
                DrawRect(new Rect2(65, 20 + line * 7, line == 3 ? 22 : 36, 2), CardBorder);
            }
        }

        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}
