using System;
using System.Collections.Generic;
using Godot;

namespace SpacioMagus;

public partial class MenuScreen : CanvasLayer
{
    private const int ParchmentMargin = 10;
    private const int ScreenMargin = 10;
    private const int InnerMargin = 16;
    private const float ButtonWidth = 160f;
    private const float ButtonHeight = 28f;
    private const float IllustrationWidth = 224f;
    private const float IllustrationHeight = 128f;
    private static readonly Color Backdrop = new(0.07f, 0.05f, 0.1f, 0.9f);

    public static MenuScreen Create(string title, string body, params (string Text, Action Action)[] buttons) =>
        Create(title, body, Array.Empty<Texture2D>(), buttons);

    public static MenuScreen Create(
        string title,
        string body,
        IReadOnlyList<Texture2D> icons,
        params (string Text, Action Action)[] buttons) =>
        Build(title, body, icons, null, null, buttons);

    public static MenuScreen CreateWithControl(
        string title,
        string body,
        Control control,
        params (string Text, Action Action)[] buttons) =>
        Build(title, body, Array.Empty<Texture2D>(), null, control, buttons);

    public static MenuScreen CreateIllustrated(
        string title,
        string body,
        Control illustration,
        params (string Text, Action Action)[] buttons) =>
        Build(title, body, Array.Empty<Texture2D>(), illustration, null, buttons);

    private static MenuScreen Build(
        string title,
        string body,
        IReadOnlyList<Texture2D> icons,
        Control? illustration,
        Control? control,
        (string Text, Action Action)[] buttons)
    {
        var screen = new MenuScreen { Layer = 10 };

        var root = new Control { Theme = GuiAssets.Theme };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        screen.AddChild(root);

        var background = new ColorRect { Color = Backdrop };
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(background);

        var parchment = GuiAssets.Panel(GuiAssets.Parchment, ParchmentMargin);
        parchment.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        parchment.OffsetLeft = ScreenMargin;
        parchment.OffsetTop = ScreenMargin;
        parchment.OffsetRight = -ScreenMargin;
        parchment.OffsetBottom = -ScreenMargin;
        root.AddChild(parchment);

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, InnerMargin);
        }

        parchment.AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        margin.AddChild(box);

        if (illustration is not null)
        {
            illustration.CustomMinimumSize = new Vector2(IllustrationWidth, IllustrationHeight);
            illustration.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            box.AddChild(illustration);
        }

        if (icons.Count > 0)
        {
            var iconRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            iconRow.AddThemeConstantOverride("separation", 4);
            foreach (var icon in icons)
            {
                iconRow.AddChild(new TextureRect { Texture = icon, StretchMode = TextureRect.StretchModeEnum.KeepCentered });
            }

            box.AddChild(iconRow);
        }

        var titleLabel = new Label
        {
            Text = title,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        titleLabel.AddThemeFontSizeOverride("font_size", GuiAssets.TitleFontSize);
        titleLabel.AddThemeColorOverride("font_color", GuiAssets.TextOnParchment);
        box.AddChild(titleLabel);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        box.AddChild(scroll);

        var bodyLabel = new Label
        {
            Text = body,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        bodyLabel.AddThemeColorOverride("font_color", GuiAssets.TextOnParchment);
        scroll.AddChild(bodyLabel);

        if (control is not null)
        {
            control.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            box.AddChild(control);
        }

        var row = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        row.AddThemeConstantOverride("separation", 6);
        box.AddChild(row);

        foreach (var (text, action) in buttons)
        {
            var button = new Button { Text = text, CustomMinimumSize = new Vector2(ButtonWidth, ButtonHeight) };
            button.Pressed += action;
            row.AddChild(button);
        }

        return screen;
    }
}
