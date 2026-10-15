using System;
using Godot;

namespace SpacioMagus;

public static class GuiAssets
{
    public const int FontSize = 16;
    public const int TitleFontSize = 32;
    public const int NumberOutlineSize = 2;
    public const int GemSize = 15;
    public const int IconSize = 16;

    public static readonly Color TextOnWood = new(0.95f, 0.89f, 0.74f);
    public static readonly Color TextOnParchment = new(0.29f, 0.18f, 0.12f);
    public static readonly Color ButtonText = new(0.08f, 0.18f, 0.13f);
    public static readonly Color AlertOutline = new(0.12f, 0.06f, 0.05f);

    private static FontFile? _font;
    private static Theme? _theme;

    public static FontFile Font => _font ??= LoadFont();

    public static Texture2D Plank { get; } = Load("res://assets/gui/bandeau.png");

    public static Texture2D Parchment { get; } = Load("res://assets/gui/parchemin.png");

    public static Texture2D Button { get; } = Load("res://assets/gui/bouton.png");

    public static Texture2D Gems { get; } = Load("res://assets/gui/gemme.png");

    public static Texture2D Icons { get; } = Load("res://assets/gui/icones.png");

    public static Texture2D Portrait { get; } = Load("res://assets/gui/portrait.png");

    public static Texture2D Star { get; } = Load("res://assets/gui/etoile.png");

    public static Texture2D Cross { get; } = Load("res://assets/gui/croix.png");

    public static Texture2D VictorySheet { get; } = Load("res://assets/gui/victoire.png");

    public static Texture2D LevelsSheet { get; } = Load("res://assets/gui/niveaux.png");

    public static Texture2D VolumeSheet { get; } = Load("res://assets/gui/volume.png");

    public static readonly Rect2 BigStarFull = new(161, 386, 29, 28);
    public static readonly Rect2 BigStarEmpty = new(241, 386, 29, 28);
    public static readonly Rect2 SmallStarFull = new(199, 392, 17, 17);
    public static readonly Rect2 SmallStarEmpty = new(279, 392, 17, 17);
    public static readonly Rect2 LevelPanel = new(7, 0, 114, 133);
    public static readonly Rect2 LevelPanelHeaderPatch = new(30, 2, 12, 11);
    public static readonly Vector2 LevelPanelCloseOffset = new(100, 2);
    public static readonly Rect2 DoorCompleted = new(168, 162, 17, 19);
    public static readonly Rect2 DoorNext = new(264, 162, 17, 19);
    public static readonly Rect2 DoorLocked = new(296, 162, 17, 27);
    public static readonly Rect2 MiniStarFull = new(165, 182, 7, 7);
    public static readonly Rect2 MiniStarEmpty = new(205, 182, 7, 7);
    public static readonly Rect2 NextMarker = new(268, 183, 9, 6);
    public static readonly Rect2 VolumeTickFull = new(0, 0, 3, 11);
    public static readonly Rect2 VolumeTickEmpty = new(3, 0, 3, 11);
    public static readonly Rect2 MusicIconOff = new(6, 0, 13, 13);
    public static readonly Rect2 MusicIconOn = new(19, 0, 13, 13);
    public static readonly Rect2 EffectsIconOff = new(32, 0, 13, 12);
    public static readonly Rect2 EffectsIconOn = new(45, 0, 13, 12);

    public static Theme Theme => _theme ??= BuildTheme();

    public static AtlasTexture Region(Texture2D texture, Rect2 region) => new() { Atlas = texture, Region = region };

    public static NinePatchRect Panel(Texture2D texture, int margin)
    {
        return new NinePatchRect
        {
            Texture = texture,
            PatchMarginLeft = margin,
            PatchMarginRight = margin,
            PatchMarginTop = margin,
            PatchMarginBottom = margin,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
    }

    public static void DrawNumber(CanvasItem canvas, int value, Vector2 center)
    {
        var text = Math.Max(value, 0).ToString();
        var font = Font;
        var width = font.GetStringSize(text, HorizontalAlignment.Left, -1, FontSize).X;
        var baseline = center.Y + (font.GetAscent(FontSize) - font.GetDescent(FontSize)) / 2f;
        var origin = new Vector2(Mathf.Round(center.X - width / 2f), Mathf.Round(baseline));
        canvas.DrawStringOutline(font, origin, text, HorizontalAlignment.Left, -1, FontSize, NumberOutlineSize, AlertOutline);
        canvas.DrawString(font, origin, text, HorizontalAlignment.Left, -1, FontSize, TextOnWood);
    }

    private static Texture2D Load(string path) =>
        GD.Load<Texture2D>(path) ?? throw new InvalidOperationException($"Texture introuvable : {path}");

    private static FontFile LoadFont()
    {
        var font = GD.Load<FontFile>("res://assets/fonts/DigitalDisco.ttf")
            ?? throw new InvalidOperationException("Police introuvable : res://assets/fonts/DigitalDisco.ttf");
        font.Antialiasing = TextServer.FontAntialiasing.None;
        font.Hinting = TextServer.Hinting.None;
        font.SubpixelPositioning = TextServer.SubpixelPositioning.Disabled;
        return font;
    }

    private static Theme BuildTheme()
    {
        var theme = new Theme { DefaultFont = Font, DefaultFontSize = FontSize };
        var states = new (string Name, int Row)[] { ("normal", 0), ("pressed", 1), ("hover", 2), ("hover_pressed", 3), ("disabled", 0) };
        foreach (var (name, row) in states)
        {
            var style = new StyleBoxTexture
            {
                Texture = Button,
                RegionRect = new Rect2(0, row * 12, 56, 12),
                TextureMarginLeft = 4,
                TextureMarginRight = 4,
                TextureMarginTop = 4,
                TextureMarginBottom = 4,
                ContentMarginLeft = 10,
                ContentMarginRight = 10,
                ContentMarginTop = 4,
                ContentMarginBottom = 6,
            };
            theme.SetStylebox(name, "Button", style);
            theme.SetStylebox(name, "OptionButton", style);
        }

        theme.SetStylebox("focus", "OptionButton", new StyleBoxEmpty());
        theme.SetStylebox("panel", "PopupMenu", new StyleBoxTexture
        {
            Texture = Parchment,
            TextureMarginLeft = 10,
            TextureMarginRight = 10,
            TextureMarginTop = 10,
            TextureMarginBottom = 10,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
        });
        theme.SetColor("font_color", "PopupMenu", TextOnParchment);
        theme.SetColor("font_hover_color", "PopupMenu", ButtonText);

        theme.SetStylebox("focus", "Button", new StyleBoxEmpty());
        foreach (var color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
        {
            theme.SetColor(color, "Button", ButtonText);
            theme.SetColor(color, "OptionButton", ButtonText);
        }

        theme.SetColor("font_disabled_color", "Button", new Color(ButtonText, 0.5f));
        theme.SetColor("font_color", "Label", TextOnWood);
        return theme;
    }
}
