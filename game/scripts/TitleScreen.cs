using System;
using Godot;

namespace SpacioMagus;

public partial class TitleScreen : CanvasLayer
{
    private const float PanelWidth = 212f;
    private const int PanelMargin = 10;
    private const int InnerMargin = 12;
    private const int ScreenMargin = 10;
    private const int TitleTop = 18;
    private const int TitleOutline = 4;
    private const float ButtonWidth = 160f;
    private const float ButtonHeight = 28f;
    private static readonly Color DisabledModulate = new(0.75f, 0.75f, 0.75f);

    public static TitleScreen Create(bool canContinue, Action newGame, Action continueGame, Action levels, Action settings, Action credits)
    {
        var screen = new TitleScreen { Layer = 10 };

        var root = new Control { Theme = GuiAssets.Theme };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        screen.AddChild(root);

        var scene = new TitleScene();
        scene.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(scene);

        var title = new Label
        {
            Text = Texts.GameTitle,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        title.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        title.OffsetTop = TitleTop;
        title.AddThemeFontSizeOverride("font_size", GuiAssets.TitleFontSize);
        title.AddThemeColorOverride("font_color", GuiAssets.TextOnWood);
        title.AddThemeColorOverride("font_outline_color", GuiAssets.AlertOutline);
        title.AddThemeConstantOverride("outline_size", TitleOutline);
        root.AddChild(title);

        var panel = new PanelContainer
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 1f,
            AnchorBottom = 1f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Begin,
            OffsetLeft = -PanelWidth / 2f,
            OffsetRight = PanelWidth / 2f,
            OffsetBottom = -ScreenMargin,
        };
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
        root.AddChild(panel);

        var buttons = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        buttons.AddThemeConstantOverride("separation", 5);
        panel.AddChild(buttons);

        AddButton(buttons, Texts.NewGameButton, newGame, true);
        AddButton(buttons, Texts.ContinueButton, continueGame, canContinue);
        AddButton(buttons, Texts.LevelsButton, levels, true);
        AddButton(buttons, Texts.SettingsButton, settings, true);
        AddButton(buttons, Texts.CreditsButton, credits, true);
        return screen;
    }

    private static void AddButton(VBoxContainer box, string text, Action action, bool enabled)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(ButtonWidth, ButtonHeight),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            Disabled = !enabled,
            Modulate = enabled ? Colors.White : DisabledModulate,
        };
        button.Pressed += action;
        box.AddChild(button);
    }
}
