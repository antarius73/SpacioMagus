using System;
using System.Collections.Generic;
using Godot;

namespace SpacioMagus;

public partial class MenuScreen : CanvasLayer
{
    private const int ParchmentMargin = 10;
    private const int InnerMargin = 16;
    private const float PanelWidth = 268f;
    private const float IllustrationGap = 10f;
    private const float IconGap = 8f;
    private const float TitleGap = 12f;
    private const float SectionGap = 16f;
    private const float ButtonsGap = 20f;
    private const int ButtonSeparation = 6;
    private const float BackGap = 18f;
    private const float ButtonWidth = 160f;
    private const float ButtonHeight = 28f;
    private const float IllustrationWidth = 224f;
    private const float IllustrationHeight = 128f;
    private const int ScreenMarginX = 10;
    private const int ScreenMarginY = 24;
    private const float ScrollHeight = 300f;
    private const int ScrollBarWidth = 4;
    private const int ScrollTextGap = 8;
    private static readonly Color Backdrop = new(0.07f, 0.05f, 0.1f, 0.9f);
    private static readonly Color ScrollTrack = new(0.36f, 0.24f, 0.15f, 0.15f);

    public static MenuScreen Create(string title, string body, params (string Text, Action Action)[] buttons) =>
        Create(title, body, Array.Empty<Texture2D>(), buttons);

    public static MenuScreen Create(
        string title,
        string body,
        IReadOnlyList<Texture2D> icons,
        params (string Text, Action Action)[] buttons) =>
        Build(title, body, icons, null, null, false, buttons);

    public static MenuScreen CreateScrolling(string title, string body, params (string Text, Action Action)[] buttons) =>
        Build(title, body, Array.Empty<Texture2D>(), null, null, true, buttons);

    public static MenuScreen CreateWithControl(
        string title,
        string body,
        Control control,
        params (string Text, Action Action)[] buttons) =>
        Build(title, body, Array.Empty<Texture2D>(), null, control, false, buttons);

    public static MenuScreen CreateIllustrated(
        string title,
        string body,
        Control illustration,
        params (string Text, Action Action)[] buttons) =>
        Build(title, body, Array.Empty<Texture2D>(), illustration, null, false, buttons);

    private static MenuScreen Build(
        string title,
        string body,
        IReadOnlyList<Texture2D> icons,
        Control? illustration,
        Control? control,
        bool scrollBody,
        (string Text, Action Action)[] buttons)
    {
        var screen = new MenuScreen { Layer = 10 };

        var root = new Control { Theme = GuiAssets.Theme };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        screen.AddChild(root);

        var background = new ColorRect { Color = Backdrop };
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(background);

        var margins = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        margins.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margins.AddThemeConstantOverride("margin_left", ScreenMarginX);
        margins.AddThemeConstantOverride("margin_right", ScreenMarginX);
        margins.AddThemeConstantOverride("margin_top", ScreenMarginY);
        margins.AddThemeConstantOverride("margin_bottom", ScreenMarginY);

        root.AddChild(margins);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        margins.AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(PanelWidth, 0) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxTexture
        {
            Texture = GuiAssets.Parchment,
            TextureMarginLeft = ParchmentMargin,
            TextureMarginRight = ParchmentMargin,
            TextureMarginTop = ParchmentMargin,
            TextureMarginBottom = ParchmentMargin,
            ContentMarginLeft = InnerMargin,
            ContentMarginRight = InnerMargin,
            ContentMarginTop = InnerMargin,
            ContentMarginBottom = InnerMargin,
        });
        center.AddChild(panel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 0);
        panel.AddChild(box);

        if (illustration is not null)
        {
            illustration.CustomMinimumSize = new Vector2(IllustrationWidth, IllustrationHeight);
            illustration.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            box.AddChild(illustration);
            box.AddChild(Spacer(IllustrationGap));
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
            box.AddChild(Spacer(IconGap));
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

        if (!string.IsNullOrWhiteSpace(body))
        {
            box.AddChild(Spacer(TitleGap));
            var bodyLabel = new Label
            {
                Text = body,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            bodyLabel.AddThemeColorOverride("font_color", GuiAssets.TextOnParchment);
            box.AddChild(scrollBody ? Scrolling(bodyLabel) : bodyLabel);
        }

        if (control is not null)
        {
            box.AddChild(Spacer(SectionGap));
            control.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            box.AddChild(control);
        }

        if (buttons.Length > 0)
        {
            box.AddChild(Spacer(ButtonsGap));
            var row = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
            row.AddThemeConstantOverride("separation", ButtonSeparation);
            box.AddChild(row);
            for (var index = 0; index < buttons.Length; index++)
            {
                var (text, action) = buttons[index];
                if (index > 0 && index == buttons.Length - 1 && text == Texts.BackButton)
                {
                    row.AddChild(Spacer(BackGap - ButtonSeparation));
                }

                var button = new Button { Text = text, CustomMinimumSize = new Vector2(ButtonWidth, ButtonHeight) };
                button.Pressed += () =>
                {
                    Sounds.Play(Sounds.ButtonKey(text));
                    action();
                };
                row.AddChild(button);
            }
        }

        return screen;
    }

    private static Control Scrolling(Control content)
    {
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, ScrollHeight),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
        };

        var bar = scroll.GetVScrollBar();
        bar.CustomMinimumSize = new Vector2(ScrollBarWidth, 0);
        bar.AddThemeStyleboxOverride("scroll", new StyleBoxFlat { BgColor = ScrollTrack });
        bar.AddThemeStyleboxOverride("scroll_focus", new StyleBoxFlat { BgColor = ScrollTrack });
        var grabber = new StyleBoxFlat { BgColor = GuiAssets.TextOnParchment };
        bar.AddThemeStyleboxOverride("grabber", grabber);
        bar.AddThemeStyleboxOverride("grabber_highlight", grabber);
        bar.AddThemeStyleboxOverride("grabber_pressed", grabber);

        var padding = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        padding.AddThemeConstantOverride("margin_right", ScrollTextGap);
        content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        padding.AddChild(content);
        scroll.AddChild(padding);
        return scroll;
    }

    private static Control Spacer(float height) => new() { CustomMinimumSize = new Vector2(0, height), MouseFilter = Control.MouseFilterEnum.Ignore };
}
