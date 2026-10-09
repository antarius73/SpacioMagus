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
    public static readonly Color AlertColor = new(0.93f, 0.2f, 0.18f);
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
        }

        theme.SetStylebox("focus", "Button", new StyleBoxEmpty());
        foreach (var color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
        {
            theme.SetColor(color, "Button", ButtonText);
        }

        theme.SetColor("font_disabled_color", "Button", new Color(ButtonText, 0.5f));
        theme.SetColor("font_color", "Label", TextOnWood);
        return theme;
    }
}
