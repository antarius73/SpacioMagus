using System;
using Godot;

namespace SpacioMagus;

public partial class Hud : CanvasLayer
{
    public const float TopHeight = 36f;
    public const float BottomHeight = 40f;

    private const int PlankMargin = 6;
    private const float PortraitX = 2f;
    private const float PortraitY = 3f;
    private const float GemsX = 31f;
    private const float GemsY = 10f;
    private const float TouchSize = 32f;
    private const float MessageMargin = 8f;

    private readonly MagicBar _magic = new();
    private readonly Label _message = new();

    public event Action? PausePressed;

    public event Action? HelpPressed;

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

        var icons = new HBoxContainer();
        icons.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.RightWide);
        icons.OffsetLeft = -2 * TouchSize - 4;
        icons.OffsetRight = -2;
        icons.AddThemeConstantOverride("separation", 0);
        top.AddChild(icons);

        var help = IconButton(1);
        help.Pressed += () => HelpPressed?.Invoke();
        icons.AddChild(help);

        var pause = IconButton(0);
        pause.Pressed += () => PausePressed?.Invoke();
        icons.AddChild(pause);

        var bottom = GuiAssets.Panel(GuiAssets.Plank, PlankMargin);
        bottom.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        bottom.OffsetTop = -BottomHeight;
        root.AddChild(bottom);

        _message.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _message.OffsetLeft = MessageMargin;
        _message.OffsetRight = -MessageMargin;
        _message.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _message.HorizontalAlignment = HorizontalAlignment.Center;
        _message.VerticalAlignment = VerticalAlignment.Center;
        _message.AddThemeColorOverride("font_color", GuiAssets.TextOnWood);
        bottom.AddChild(_message);
    }

    public void Refresh(int magic, int maxMagic)
    {
        _magic.SetMagic(magic, maxMagic);
    }

    public void ShowMessage(string message)
    {
        _message.Text = message;
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
