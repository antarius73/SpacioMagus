using System;
using Godot;

namespace SpacioMagus;

public partial class VolumeBar : Control
{
    private const int Ticks = 20;
    private const int TickPitch = 4;
    private const int Steps = 10;
    private const int MaxValue = 100;

    private int _value;
    private bool _dragging;
    private bool _changedDuringDrag;

    public VolumeBar()
    {
        FocusMode = FocusModeEnum.All;
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(Ticks * TickPitch - 1, GuiAssets.VolumeTickFull.Size.Y);
    }

    public event Action<int>? Changed;

    public event Action? Released;

    public int Value
    {
        get => _value;
        set
        {
            var clamped = Math.Clamp(value, 0, MaxValue);
            if (clamped == _value)
            {
                return;
            }

            _value = clamped;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        var filled = (int)Math.Round(_value / (double)MaxValue * Ticks);
        var tick = GuiAssets.VolumeTickFull.Size;
        var top = Mathf.Round((Size.Y - tick.Y) / 2f);
        for (var index = 0; index < Ticks; index++)
        {
            var source = index < filled ? GuiAssets.VolumeTickFull : GuiAssets.VolumeTickEmpty;
            DrawTextureRectRegion(GuiAssets.VolumeSheet, new Rect2(index * TickPitch, top, tick.X, tick.Y), source);
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button when button.Pressed:
                _dragging = true;
                _changedDuringDrag = false;
                SetFromPosition(button.Position.X);
                AcceptEvent();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } when _dragging:
                _dragging = false;
                if (_changedDuringDrag)
                {
                    Released?.Invoke();
                }

                AcceptEvent();
                break;
            case InputEventMouseMotion motion when _dragging:
                SetFromPosition(motion.Position.X);
                AcceptEvent();
                break;
            case not null when @event.IsActionPressed("ui_left"):
                Step(-1);
                AcceptEvent();
                break;
            case not null when @event.IsActionPressed("ui_right"):
                Step(1);
                AcceptEvent();
                break;
        }
    }

    internal void SetByUser(int value)
    {
        if (Apply(value))
        {
            Released?.Invoke();
        }
    }

    private void Step(int direction)
    {
        if (Apply(_value + direction * MaxValue / Steps))
        {
            Released?.Invoke();
        }
    }

    private void SetFromPosition(float x)
    {
        var ratio = Math.Clamp(x / Math.Max(1f, Size.X), 0f, 1f);
        if (Apply((int)Math.Round(ratio * Steps) * MaxValue / Steps))
        {
            _changedDuringDrag = true;
        }
    }

    private bool Apply(int value)
    {
        var previous = _value;
        Value = value;
        if (_value == previous)
        {
            return false;
        }

        Changed?.Invoke(_value);
        return true;
    }
}
