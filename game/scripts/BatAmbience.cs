using System;
using System.Collections.Generic;
using Godot;

namespace SpacioMagus;

public partial class BatAmbience : Node
{
    private const string SoundKey = "mo.bat";
    private const float HearingTiles = 6f;
    private const double MinDelay = 4.0;
    private const double MaxDelay = 9.0;

    private readonly Dictionary<int, (AudioStreamPlayer Player, double NextAt)> _bats = new();
    private readonly Random _random = new();
    private AudioStream? _stream;
    private float _baseVolumeDb;
    private double _time;

    public Func<Vector2>? Listener { get; set; }

    public Func<IEnumerable<(int Id, Vector2 Position)>>? Bats { get; set; }

    private static float HearingDistance => HearingTiles * LevelView.Tile;

    public void Reset()
    {
        Clear();
        if (Sounds.Find(SoundKey) is { } sound)
        {
            _stream = sound.Stream;
            _baseVolumeDb = sound.VolumeDb;
        }
    }

    public void Clear()
    {
        foreach (var (player, _) in _bats.Values)
        {
            player.QueueFree();
        }

        _bats.Clear();
        _stream = null;
    }

    public override void _Process(double delta)
    {
        _time += delta;
        if (_stream is null || Bats is null || Listener is null)
        {
            return;
        }

        if (!Sounds.Enabled || GetParent() is not CanvasItem parent || !parent.IsVisibleInTree())
        {
            return;
        }

        var origin = Listener();
        foreach (var (id, position) in Bats())
        {
            if (!_bats.TryGetValue(id, out var bat))
            {
                var player = new AudioStreamPlayer { Stream = _stream, Bus = Sounds.EffectsBus };
                AddChild(player);
                bat = (player, _time + NextDelay());
            }

            if (_time >= bat.NextAt)
            {
                var gain = Mathf.Clamp(1f - position.DistanceTo(origin) / HearingDistance, 0f, 1f);
                if (gain > 0f)
                {
                    bat.Player.VolumeDb = _baseVolumeDb + Mathf.LinearToDb(gain);
                    bat.Player.Play();
                }

                bat = (bat.Player, _time + NextDelay());
            }

            _bats[id] = bat;
        }
    }

    private double NextDelay() => MinDelay + _random.NextDouble() * (MaxDelay - MinDelay);
}
