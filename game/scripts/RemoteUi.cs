using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using SpacioMagus.Core.Remote;

namespace SpacioMagus;

internal sealed class RemoteUi
{
    private const int MaxLabelHops = 3;

    private readonly List<string> _texts = new();
    private readonly List<Entry> _entries = new();

    private RemoteUi()
    {
    }

    internal sealed record Entry(UiControl Control, Control? Focus, Func<int?, (string Error, string Message)?> Use);

    public UiView View => new(_texts.ToArray(), _entries.Select(entry => entry.Control).ToArray());

    public static RemoteUi Scan(Node? root, IEnumerable<string> notes)
    {
        var ui = new RemoteUi();
        ui._texts.AddRange(notes);
        if (root is null)
        {
            return ui;
        }

        ui.Walk(root);
        if (root is InfoPopup popup)
        {
            ui._entries.Add(new Entry(
                new UiControl($"{popup.GetInstanceId()}:fermer", UiKind.Close, "Fermer", true),
                null,
                _ =>
                {
                    popup.Close();
                    return null;
                }));
        }

        return ui;
    }

    public Entry? Find(string id) => _entries.FirstOrDefault(entry => entry.Control.Id == id);

    public static (string Error, string Message)? Validate(UiControl control, int? value)
    {
        if (!control.Enabled)
        {
            return (RemoteErrors.ControlDisabled, $"« {control.Label} » est désactivé.");
        }

        if (control.Kind is not (UiKind.Choice or UiKind.Slider))
        {
            return null;
        }

        if (value is not { } number)
        {
            return (RemoteErrors.InvalidValue, $"« {control.Label} » exige une valeur ({control.Min} à {control.Max}).");
        }

        return number < control.Min || number > control.Max
            ? (RemoteErrors.InvalidValue, $"Valeur {number} hors limites pour « {control.Label} » ({control.Min} à {control.Max}).")
            : null;
    }

    private void Walk(Node node)
    {
        if (node is CanvasItem item && !item.IsVisibleInTree())
        {
            return;
        }

        switch (node)
        {
            case OptionButton choice:
                AddChoice(choice);
                return;
            case Button button:
                AddButton(button, button.Text);
                return;
            case TextureButton texture:
                AddButton(texture, string.IsNullOrWhiteSpace(texture.TooltipText) ? texture.Name.ToString() : texture.TooltipText);
                return;
            case VolumeBar bar:
                AddSlider(bar);
                return;
            case LevelGrid grid:
                AddDoors(grid);
                break;
            case Label label:
                AddText(label.Text);
                break;
            case RichTextLabel rich:
                AddText(rich.GetParsedText());
                break;
        }

        foreach (var child in node.GetChildren())
        {
            Walk(child);
        }
    }

    private void AddText(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length > 0)
        {
            _texts.Add(trimmed);
        }
    }

    private void AddButton(BaseButton button, string label)
    {
        _entries.Add(new Entry(
            new UiControl(button.GetInstanceId().ToString(), UiKind.Button, label, !button.Disabled),
            button,
            _ =>
            {
                button.EmitSignal(BaseButton.SignalName.Pressed);
                return null;
            }));
    }

    private void AddChoice(OptionButton choice)
    {
        var options = Enumerable.Range(0, choice.ItemCount).Select(index => choice.GetItemText(index)).ToArray();
        _entries.Add(new Entry(
            new UiControl(
                choice.GetInstanceId().ToString(),
                UiKind.Choice,
                LabelBefore(choice),
                !choice.Disabled && options.Length > 0,
                options,
                choice.Selected,
                0,
                Math.Max(0, options.Length - 1)),
            choice,
            value =>
            {
                var index = value!.Value;
                choice.Select(index);
                choice.EmitSignal(OptionButton.SignalName.ItemSelected, (long)index);
                return null;
            }));
    }

    private void AddSlider(VolumeBar bar)
    {
        _entries.Add(new Entry(
            new UiControl(bar.GetInstanceId().ToString(), UiKind.Slider, LabelBefore(bar), true, null, bar.Value, 0, 100),
            bar,
            value =>
            {
                bar.SetByUser(value!.Value);
                return null;
            }));
    }

    private void AddDoors(LevelGrid grid)
    {
        foreach (var (index, level) in grid.VisibleDoors())
        {
            var state = level.State switch
            {
                DoorState.Completed => $"terminé, {level.Stars}/3 étoiles",
                DoorState.Next => "à faire",
                _ => "verrouillé",
            };
            _entries.Add(new Entry(
                new UiControl($"{grid.GetInstanceId()}:{index}", UiKind.Door, $"Niveau {index + 1} ({state})", level.State != DoorState.Locked),
                null,
                _ => grid.Choose(index) ? null : (RemoteErrors.ControlDisabled, $"Le niveau {index + 1} est verrouillé.")));
        }
    }

    private static string LabelBefore(Node node)
    {
        var current = node;
        for (var hop = 0; hop < MaxLabelHops && current.GetParent() is { } parent; hop++)
        {
            var index = current.GetIndex();
            for (var sibling = index - 1; sibling >= 0; sibling--)
            {
                if (FirstLabel(parent.GetChild(sibling)) is { } text)
                {
                    return text;
                }
            }

            current = parent;
        }

        return string.Empty;
    }

    private static string? FirstLabel(Node node)
    {
        if (node is Label label && label.Text.Trim().Length > 0)
        {
            return label.Text.Trim();
        }

        foreach (var child in node.GetChildren())
        {
            if (FirstLabel(child) is { } text)
            {
                return text;
            }
        }

        return null;
    }
}
