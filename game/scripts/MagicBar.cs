using Godot;

namespace SpacioMagus;

public partial class MagicBar : Control
{
    private const int Step = 13;

    private int _magic;
    private int _max;

    public void SetMagic(int magic, int max)
    {
        _magic = magic;
        _max = max;
        CustomMinimumSize = new Vector2(GuiAssets.GemSize + Step * (max - 1), GuiAssets.GemSize);
        QueueRedraw();
    }

    public override void _Draw()
    {
        for (var index = 0; index < _max; index++)
        {
            var source = new Rect2(index < _magic ? 0 : GuiAssets.GemSize, 0, GuiAssets.GemSize, GuiAssets.GemSize);
            DrawTextureRectRegion(GuiAssets.Gems, new Rect2(index * Step, 0, GuiAssets.GemSize, GuiAssets.GemSize), source);
        }
    }
}
