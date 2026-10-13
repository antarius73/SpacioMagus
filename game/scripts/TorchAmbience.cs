using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Cell = SpacioMagus.Core.Position;

namespace SpacioMagus;

public partial class TorchAmbience : Node
{
    private const string SoundKey = "dc.torch";
    private const int MaxVoices = 4;
    private const float HearingTiles = 6f;
    private const float SilentDb = -60f;
    private const float FadeDbPerSecond = 40f;

    private readonly List<(Vector2 Position, AudioStreamPlayer Player)> _sources = new();
    private readonly Random _random = new();
    private float _baseVolumeDb;

    public Func<Vector2>? Listener { get; set; }

    private static float HearingDistance => HearingTiles * LevelView.Tile;

    public void ShowTorches(IEnumerable<Cell> cells)
    {
        Clear();
        if (Sounds.Find(SoundKey) is not { } sound)
        {
            return;
        }

        _baseVolumeDb = sound.VolumeDb;
        foreach (var cell in cells.Distinct())
        {
            var player = new AudioStreamPlayer { Stream = sound.Stream, VolumeDb = SilentDb, Bus = Sounds.EffectsBus };
            AddChild(player);
            _sources.Add((LevelView.CellCenter(cell), player));
        }
    }

    public void Clear()
    {
        foreach (var (_, player) in _sources)
        {
            player.QueueFree();
        }

        _sources.Clear();
    }

    public override void _Process(double delta)
    {
        if (_sources.Count == 0)
        {
            return;
        }

        var active = Sounds.Enabled && Listener is not null && GetParent() is CanvasItem { Visible: true } parent && parent.IsVisibleInTree();
        var origin = active ? Listener!() : Vector2.Zero;
        var nearest = active
            ? _sources
                .Where(source => source.Position.DistanceTo(origin) < HearingDistance)
                .OrderBy(source => source.Position.DistanceTo(origin))
                .Take(MaxVoices)
                .Select(source => source.Player)
                .ToHashSet()
            : new HashSet<AudioStreamPlayer>();

        var step = (float)delta * FadeDbPerSecond;
        foreach (var (position, player) in _sources)
        {
            var target = nearest.Contains(player) ? TargetDb(position.DistanceTo(origin)) : SilentDb;
            if (target > SilentDb && !player.Playing)
            {
                player.VolumeDb = SilentDb;
                player.Play((float)(_random.NextDouble() * (player.Stream?.GetLength() ?? 0)));
            }

            player.VolumeDb = Mathf.MoveToward(player.VolumeDb, target, step);
            if (player.Playing && target <= SilentDb && player.VolumeDb <= SilentDb)
            {
                player.Stop();
            }
        }
    }

    private float TargetDb(float distance)
    {
        var gain = Mathf.Clamp(1f - distance / HearingDistance, 0f, 1f);
        return gain <= 0f ? SilentDb : _baseVolumeDb + Mathf.LinearToDb(gain);
    }
}
