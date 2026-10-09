using System;
using Godot;

namespace SpacioMagus;

public enum Discovery
{
    Door,
    Potion,
    GreenBlob,
}

public partial class DiscoveryPopup : CanvasLayer
{
    private const int ScreenMargin = 16;
    private const int TextureMargin = 10;
    private const float ContentWidth = 224f;
    private const float IllustrationHeight = 128f;
    private const float ButtonWidth = 140f;
    private const float ButtonHeight = 28f;
    private static readonly Color Backdrop = new(0.04f, 0.03f, 0.07f, 0.6f);

    public event Action<bool>? Closed;

    private CheckBox _hideTips = null!;

    public static DiscoveryPopup Create(Discovery discovery)
    {
        var (title, body, illustration) = discovery switch
        {
            Discovery.Door => (Texts.DoorTitle, Texts.Door, (Control)new DoorIllustration()),
            Discovery.Potion => (Texts.PotionTitle, Texts.Potion, (Control)new PotionIllustration()),
            _ => (Texts.GreenBlobTitle, Texts.GreenBlob, (Control)new BlobIllustration()),
        };

        var popup = new DiscoveryPopup { Layer = 9 };

        var root = new Control { Theme = GuiAssets.Theme };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        popup.AddChild(root);

        var backdrop = new ColorRect { Color = Backdrop, MouseFilter = Control.MouseFilterEnum.Stop };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
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

        var panel = new PanelContainer();
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

        var box = new VBoxContainer();
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

        popup._hideTips = new CheckBox { Text = Texts.HideTips, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        foreach (var color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
        {
            popup._hideTips.AddThemeColorOverride(color, GuiAssets.TextOnParchment);
        }

        box.AddChild(popup._hideTips);

        var button = new Button
        {
            Text = Texts.DiscoveryButton,
            CustomMinimumSize = new Vector2(ButtonWidth, ButtonHeight),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        };
        button.Pressed += popup.Close;
        box.AddChild(button);

        return popup;
    }

    private void Close()
    {
        QueueFree();
        Closed?.Invoke(_hideTips.ButtonPressed);
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
    private const double FrameDuration = 0.2;
    private const int Frames = 4;
    private const int Row = 160;
    private static readonly Vector2 SceneSize = new(32, 32);

    private readonly Texture2D _animations = Load("res://assets/sprites/animations.png");

    public override void _Draw()
    {
        BeginScene(SceneSize, 3f);
        var frame = (int)(Time / FrameDuration) % Frames;
        DrawTextureRectRegion(_animations, new Rect2(0, 0, 32, 32), new Rect2(frame * 32, Row, 32, 32));
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}
