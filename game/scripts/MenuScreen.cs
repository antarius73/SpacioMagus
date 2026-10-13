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

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

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
            box.AddChild(bodyLabel);
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
                button.Pressed += action;
                row.AddChild(button);
            }
        }

        return screen;
    }

    private static Control Spacer(float height) => new() { CustomMinimumSize = new Vector2(0, height), MouseFilter = Control.MouseFilterEnum.Ignore };
}
