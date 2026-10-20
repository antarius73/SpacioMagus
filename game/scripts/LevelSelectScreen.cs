using System;
using System.Collections.Generic;
using Godot;

namespace SpacioMagus;

public enum DoorState
{
    Completed,
    Next,
    Locked,
}

public sealed record LevelEntry(DoorState State, int Stars);

public partial class LevelSelectScreen : CanvasLayer
{
    private static readonly Color Backdrop = new(0.07f, 0.05f, 0.1f, 0.6f);

    public static LevelSelectScreen Create(IReadOnlyList<LevelEntry> levels, int page, Action<int> choose, Action<int> changePage, Action back)
    {
        var screen = new LevelSelectScreen { Layer = 10 };
        var root = new Control { Theme = GuiAssets.Theme };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        screen.AddChild(root);

        var scene = new TitleScene();
        scene.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(scene);

        var backdrop = new ColorRect { Color = Backdrop, MouseFilter = Control.MouseFilterEnum.Ignore };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(backdrop);

        var pages = Math.Max(1, (levels.Count + LevelGrid.PerPage - 1) / LevelGrid.PerPage);
        var grid = new LevelGrid(levels, Math.Clamp(page, 0, pages - 1), pages, choose, changePage, back)
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -LevelGrid.PanelSize.X / 2f,
            OffsetRight = LevelGrid.PanelSize.X / 2f,
            OffsetTop = -LevelGrid.PanelSize.Y / 2f,
            OffsetBottom = LevelGrid.PanelSize.Y / 2f,
        };
        root.AddChild(grid);
        return screen;
    }
}

public partial class LevelGrid : Control
{
    public const int PerPage = 9;
    public const float Scale = 2f;
    public static readonly Vector2 PanelSize = new(GuiAssets.LevelPanel.Size.X * Scale, 350);

    private const float TopSlice = 40f;
    private const float BottomSlice = 40f;
    private const int StretchRow = 11;
    private const float HeaderExtra = 10f;
    private const float HeaderHeight = 28f + HeaderExtra;
    private const float GridLeft = 22f;
    private const float GridTop = 54f;
    private const float CellWidth = 64f;
    private const float CellHeight = 68f;
    private const float StarsTop = 40f;
    private const float StarSpacing = 16f;
    private const float NumberTop = 18f;
    private const float ButtonWidth = 120f;
    private const float PageButtonWidth = 36f;
    private const float ButtonHeight = 28f;
    private const float ButtonBottom = 22f;
    private const float ButtonGap = 8f;

    private readonly IReadOnlyList<LevelEntry> _levels;
    private readonly int _page;
    private readonly Action<int> _choose;

    public LevelGrid(IReadOnlyList<LevelEntry> levels, int page, int pages, Action<int> choose, Action<int> changePage, Action back)
    {
        _levels = levels;
        _page = page;
        _choose = choose;
        MouseFilter = MouseFilterEnum.Stop;

        var top = PanelSize.Y - ButtonBottom - ButtonHeight;
        var backButton = new Button
        {
            Text = Texts.BackButton,
            Position = new Vector2((PanelSize.X - ButtonWidth) / 2f, top),
            Size = new Vector2(ButtonWidth, ButtonHeight),
        };
        backButton.Pressed += () =>
        {
            Sounds.Play("ui.back");
            back();
        };
        AddChild(backButton);

        if (pages <= 1)
        {
            return;
        }

        AddChild(PageButton("<", page > 0, () => changePage(page - 1), new Vector2(backButton.Position.X - ButtonGap - PageButtonWidth, top)));
        AddChild(PageButton(">", page < pages - 1, () => changePage(page + 1), new Vector2(backButton.Position.X + ButtonWidth + ButtonGap, top)));
    }

    public override void _Draw()
    {
        var sheet = GuiAssets.LevelsSheet;
        var panel = GuiAssets.LevelPanel;
        var width = PanelSize.X;
        var top = TopSlice + HeaderExtra;
        var bottomSource = new Rect2(panel.Position.X, panel.End.Y - BottomSlice / Scale, panel.Size.X, BottomSlice / Scale);
        var middleTop = panel.Position.Y + TopSlice / Scale;
        var middleSource = new Rect2(panel.Position.X, middleTop, panel.Size.X, bottomSource.Position.Y - middleTop);
        DrawTop(panel.Position.X, panel.Size.X, 0);
        DrawTextureRectRegion(sheet, new Rect2(0, top, width, PanelSize.Y - top - BottomSlice), middleSource);
        DrawTextureRectRegion(sheet, new Rect2(0, PanelSize.Y - BottomSlice, width, BottomSlice), bottomSource);
        DrawTop(
            GuiAssets.LevelPanelHeaderPatch.Position.X,
            GuiAssets.LevelPanelHeaderPatch.Size.X,
            GuiAssets.LevelPanelCloseOffset.X * Scale);
        DrawTitle();

        for (var slot = 0; slot < PerPage; slot++)
        {
            var index = _page * PerPage + slot;
            if (index >= _levels.Count)
            {
                break;
            }

            var level = _levels[index];
            var door = DoorRect(slot);
            var source = level.State switch
            {
                DoorState.Completed => GuiAssets.DoorCompleted,
                DoorState.Next => GuiAssets.DoorNext,
                _ => GuiAssets.DoorLocked,
            };
            DrawTextureRectRegion(sheet, new Rect2(door.Position, source.Size * Scale), source);
            if (level.State == DoorState.Locked)
            {
                continue;
            }

            var center = door.Position.X + door.Size.X / 2f;
            GuiAssets.DrawNumber(this, index + 1, new Vector2(center, door.Position.Y + NumberTop));
            if (level.State == DoorState.Completed)
            {
                for (var star = 0; star < 3; star++)
                {
                    var region = star < level.Stars ? GuiAssets.MiniStarFull : GuiAssets.MiniStarEmpty;
                    var position = new Vector2(center - StarSpacing * 1.5f + star * StarSpacing, door.Position.Y + StarsTop);
                    DrawTextureRectRegion(sheet, new Rect2(position, region.Size * Scale), region);
                }
            }
            else
            {
                var marker = GuiAssets.NextMarker;
                var size = marker.Size * Scale;
                DrawTextureRectRegion(sheet, new Rect2(new Vector2(center - size.X / 2f, door.Position.Y + StarsTop + 2), size), marker);
            }
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click)
        {
            return;
        }

        for (var slot = 0; slot < PerPage; slot++)
        {
            var index = _page * PerPage + slot;
            if (index >= _levels.Count)
            {
                break;
            }

            if (!DoorRect(slot).Grow(6).HasPoint(click.Position))
            {
                continue;
            }

            AcceptEvent();
            Choose(index);
            return;
        }
    }

    internal IEnumerable<(int Index, LevelEntry Level)> VisibleDoors()
    {
        for (var slot = 0; slot < PerPage; slot++)
        {
            var index = _page * PerPage + slot;
            if (index >= _levels.Count)
            {
                yield break;
            }

            yield return (index, _levels[index]);
        }
    }

    internal bool Choose(int index)
    {
        if (index < 0 || index >= _levels.Count)
        {
            return false;
        }

        if (_levels[index].State == DoorState.Locked)
        {
            Sounds.Play("ui.locked");
            return false;
        }

        Sounds.Play("ui.confirm");
        _choose(index);
        return true;
    }

    private void DrawTop(float sourceX, float sourceWidth, float destinationX)
    {
        var sheet = GuiAssets.LevelsSheet;
        var y = GuiAssets.LevelPanel.Position.Y;
        var width = sourceWidth * Scale;
        DrawTextureRectRegion(sheet, new Rect2(destinationX, 0, width, StretchRow * Scale), new Rect2(sourceX, y, sourceWidth, StretchRow));
        DrawTextureRectRegion(sheet, new Rect2(destinationX, StretchRow * Scale, width, Scale + HeaderExtra), new Rect2(sourceX, y + StretchRow, sourceWidth, 1));
        var rest = TopSlice / Scale - StretchRow - 1;
        DrawTextureRectRegion(
            sheet,
            new Rect2(destinationX, (StretchRow + 1) * Scale + HeaderExtra, width, rest * Scale),
            new Rect2(sourceX, y + StretchRow + 1, sourceWidth, rest));
    }

    private void DrawTitle()
    {
        var font = GuiAssets.Font;
        var size = GuiAssets.TitleFontSize;
        var text = Texts.LevelsTitle;
        var textWidth = font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
        var baseline = HeaderHeight / 2f + (font.GetAscent(size) - font.GetDescent(size)) / 2f;
        var origin = new Vector2(Mathf.Round((PanelSize.X - textWidth) / 2f), Mathf.Round(baseline));
        DrawStringOutline(font, origin, text, HorizontalAlignment.Left, -1, size, 4, GuiAssets.AlertOutline);
        DrawString(font, origin, text, HorizontalAlignment.Left, -1, size, GuiAssets.TextOnWood);
    }

    private static Button PageButton(string text, bool enabled, Action action, Vector2 position)
    {
        var button = new Button
        {
            Text = text,
            Disabled = !enabled,
            Position = position,
            Size = new Vector2(PageButtonWidth, ButtonHeight),
        };
        button.Pressed += () =>
        {
            Sounds.Play("ui.tap");
            action();
        };
        return button;
    }

    private static Rect2 DoorRect(int slot)
    {
        var size = GuiAssets.DoorNext.Size * Scale;
        var cellX = GridLeft + slot % 3 * CellWidth + CellWidth / 2f;
        var cellY = GridTop + slot / 3 * CellHeight;
        return new Rect2(cellX - size.X / 2f, cellY, size.X, size.Y);
    }
}
