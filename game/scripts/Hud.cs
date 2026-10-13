using System;
using Godot;

namespace SpacioMagus;

public partial class Hud : CanvasLayer
{
    public const float TopHeight = 36f;
    public const float BottomHeight = 0f;

    private const int PlankMargin = 6;
    private const float PortraitX = 2f;
    private const float PortraitY = 3f;
    private const float GemsX = 31f;
    private const float GemsY = 10f;
    private const float TouchSize = 32f;
    private const float TurnX = 104f;
    private const float TurnY = 8f;
    private const int TurnOutline = 2;
    private const float PopupWidth = 224f;
    private const int PopupScreenMargin = 16;
    private const int PopupTextureMargin = 10;
    private const double MessageDuration = 3.0;
    private const double MessageFadeDuration = 0.25;

    private readonly MagicBar _magic = new();
    private readonly Label _turn = new();
    private readonly Label _message = new();
    private readonly PanelContainer _popup = new();
    private double _time;
    private double _hideAt = double.NegativeInfinity;

    public event Action? PausePressed;

    public override void _Ready()
    {
        var root = new Control { Theme = GuiAssets.Theme, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        var top = GuiAssets.Panel(GuiAssets.Plank, PlankMargin);
        top.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        top.OffsetBottom = TopHeight;
        root.AddChild(top);

        top.AddChild(new TextureRect
        {
            Texture = GuiAssets.Portrait,
            Position = new Vector2(PortraitX, PortraitY),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });

        _magic.Position = new Vector2(GemsX, GemsY);
        _magic.MouseFilter = Control.MouseFilterEnum.Ignore;
        top.AddChild(_magic);

        _turn.Position = new Vector2(TurnX, TurnY);
        _turn.MouseFilter = Control.MouseFilterEnum.Ignore;
        _turn.AddThemeColorOverride("font_color", GuiAssets.TextOnWood);
        _turn.AddThemeColorOverride("font_outline_color", GuiAssets.AlertOutline);
        _turn.AddThemeConstantOverride("outline_size", TurnOutline);
        top.AddChild(_turn);

        var icons = new HBoxContainer();
        icons.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.RightWide);
        icons.OffsetLeft = -TouchSize - 4;
        icons.OffsetRight = -2;
        icons.AddThemeConstantOverride("separation", 0);
        top.AddChild(icons);

        var pause = IconButton(0);
        pause.Pressed += () => PausePressed?.Invoke();
        icons.AddChild(pause);

        var style = new StyleBoxTexture
        {
            Texture = GuiAssets.Parchment,
            TextureMarginLeft = PopupTextureMargin,
            TextureMarginRight = PopupTextureMargin,
            TextureMarginTop = PopupTextureMargin,
            TextureMarginBottom = PopupTextureMargin,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
        };
        _popup.AddThemeStyleboxOverride("panel", style);
        _popup.MouseFilter = Control.MouseFilterEnum.Ignore;
        _popup.Visible = false;

        var popupArea = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        popupArea.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
        {
            popupArea.AddThemeConstantOverride(side, PopupScreenMargin);
        }

        root.AddChild(popupArea);
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        popupArea.AddChild(center);
        center.AddChild(_popup);

        _message.CustomMinimumSize = new Vector2(PopupWidth, 0);
        _message.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _message.HorizontalAlignment = HorizontalAlignment.Center;
        _message.MouseFilter = Control.MouseFilterEnum.Ignore;
        _message.AddThemeColorOverride("font_color", GuiAssets.TextOnParchment);
        _popup.AddChild(_message);
    }

    public void Refresh(int magic, int maxMagic, int turn)
    {
        _magic.SetMagic(magic, maxMagic);
        _turn.Text = Texts.TurnCounter(turn);
    }

    public void ShowMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        _message.Text = message;
        _popup.Visible = true;
        _popup.Modulate = Colors.White;
        _hideAt = _time + MessageDuration;
    }

    public void HideMessage()
    {
        _popup.Visible = false;
        _hideAt = double.NegativeInfinity;
    }

    public override void _Process(double delta)
    {
        _time += delta;
        if (!_popup.Visible)
        {
            return;
        }

        var remaining = _hideAt - _time;
        if (remaining <= 0)
        {
            HideMessage();
            return;
        }

        var alpha = (float)Math.Clamp(remaining / MessageFadeDuration, 0.0, 1.0);
        _popup.Modulate = new Color(1f, 1f, 1f, alpha);
    }

    private static TextureButton IconButton(int index)
    {
        var region = new Rect2(index * GuiAssets.IconSize, 0, GuiAssets.IconSize, GuiAssets.IconSize);
        return new TextureButton
        {
            TextureNormal = GuiAssets.Region(GuiAssets.Icons, region),
            IgnoreTextureSize = true,
            StretchMode = TextureButton.StretchModeEnum.KeepCentered,
            CustomMinimumSize = new Vector2(TouchSize, TouchSize),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
    }
}
