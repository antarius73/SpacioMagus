using System;
using Godot;

namespace SpacioMagus;

public partial class VictoryScreen : CanvasLayer
{
    private const float PanelWidth = 236f;
    private const int PanelMargin = 10;
    private const int InnerMargin = 14;
    private const float ButtonWidth = 160f;
    private const float ButtonHeight = 28f;
    private const float ButtonsGap = 14f;
    private static readonly Color Backdrop = new(0.07f, 0.05f, 0.1f, 0.75f);
    private static readonly Color RecordColor = new(0.63f, 0.27f, 0.08f);

    public static VictoryScreen Create(int stars, int turns, int? record, bool newRecord, params (string Text, Action Action)[] buttons)
    {
        var screen = new VictoryScreen { Layer = 10 };
        var root = new Control { Theme = GuiAssets.Theme };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        screen.AddChild(root);

        var backdrop = new ColorRect { Color = Backdrop };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(backdrop);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(PanelWidth, 0) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxTexture
        {
            Texture = GuiAssets.Parchment,
            TextureMarginLeft = PanelMargin,
            TextureMarginRight = PanelMargin,
            TextureMarginTop = PanelMargin,
            TextureMarginBottom = PanelMargin,
            ContentMarginLeft = InnerMargin,
            ContentMarginRight = InnerMargin,
            ContentMarginTop = InnerMargin,
            ContentMarginBottom = InnerMargin,
        });
        center.AddChild(panel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        panel.AddChild(box);

        var title = new Label { Text = Texts.VictoryTitle, HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", GuiAssets.TitleFontSize);
        title.AddThemeColorOverride("font_color", GuiAssets.TextOnParchment);
        box.AddChild(title);

        box.AddChild(new StarRow(stars, newRecord) { SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter });

        box.AddChild(Line(Texts.TurnsResult(turns), GuiAssets.TextOnParchment));
        if (newRecord)
        {
            box.AddChild(Line(Texts.NewRecord, RecordColor));
        }
        else if (record is { } best)
        {
            box.AddChild(Line(Texts.Record(best), GuiAssets.TextOnParchment));
        }

        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, ButtonsGap), MouseFilter = Control.MouseFilterEnum.Ignore });
        var row = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        row.AddThemeConstantOverride("separation", 5);
        box.AddChild(row);
        foreach (var (text, action) in buttons)
        {
            var button = new Button { Text = text, CustomMinimumSize = new Vector2(ButtonWidth, ButtonHeight) };
            button.Pressed += () =>
            {
                Sounds.Play(Sounds.ButtonKey(text));
                action();
            };
            row.AddChild(button);
        }

        return screen;
    }

    private static Label Line(string text, Color color)
    {
        var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        label.AddThemeColorOverride("font_color", color);
        return label;
    }
}

public partial class StarRow : Control
{
    private const float Scale = 2f;
    private const double FirstDelay = 0.3;
    private const double StarInterval = 0.3;
    private const double PopDuration = 0.25;
    private const float LeftOffset = 57f;
    private const float RightOffset = 23f;
    private const float BigStarHalfWidth = 29f;
    private const float SideDrop = 20f;
    private static readonly Vector2 RowSize = new(150, 64);

    private readonly int _stars;
    private readonly bool _newRecord;
    private double _time;
    private int _announcedStars;
    private bool _announcedRecord;

    public StarRow(int stars, bool newRecord)
    {
        _stars = Math.Clamp(stars, 0, 3);
        _newRecord = newRecord;
        CustomMinimumSize = RowSize;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        _time += delta;
        AnnounceStars();
        QueueRedraw();
    }

    private void AnnounceStars()
    {
        while (_announcedStars < _stars && _time >= FirstDelay + _announcedStars * StarInterval)
        {
            _announcedStars++;
            Sounds.Play("ui.star");
        }

        if (_newRecord && !_announcedRecord && _time >= FirstDelay + _stars * StarInterval + PopDuration)
        {
            _announcedRecord = true;
            Sounds.Play("ui.record");
        }
    }

    public override void _Draw()
    {
        var center = Size.X / 2f;
        DrawStar(0, false, new Vector2(center - LeftOffset, SideDrop));
        DrawStar(2, false, new Vector2(center + RightOffset, SideDrop));
        DrawStar(1, true, new Vector2(center - BigStarHalfWidth, 0));
    }

    private void DrawStar(int index, bool big, Vector2 position)
    {
        var order = index == 1 ? 1 : index == 0 ? 0 : 2;
        var earned = order < _stars;
        var empty = big ? GuiAssets.BigStarEmpty : GuiAssets.SmallStarEmpty;
        var full = big ? GuiAssets.BigStarFull : GuiAssets.SmallStarFull;
        var size = empty.Size * Scale;
        DrawTextureRectRegion(GuiAssets.VictorySheet, new Rect2(position, size), empty);
        if (!earned)
        {
            return;
        }

        var elapsed = _time - FirstDelay - order * StarInterval;
        if (elapsed < 0)
        {
            return;
        }

        var progress = (float)Math.Clamp(elapsed / PopDuration, 0.0, 1.0);
        var pop = progress < 0.6f ? progress / 0.6f * 1.25f : 1.25f - (progress - 0.6f) / 0.4f * 0.25f;
        var scaled = size * pop;
        DrawTextureRectRegion(GuiAssets.VictorySheet, new Rect2(position + (size - scaled) / 2f, scaled), full);
    }
}
