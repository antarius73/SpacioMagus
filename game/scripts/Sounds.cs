using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using SpacioMagus.Core;
using SpacioMagus.Core.Loading;

namespace SpacioMagus;

public partial class Sounds : Node
{
    private const string SoundsDirectory = "res://assets/sons";
    private const string TableFile = "sons.txt";
    private const int Voices = 8;
    private const float VariantPitch = 1.06f;
    private const float MusicFadeSeconds = 0.6f;
    private const float SilentDb = -60f;

    public const string EffectsBus = "Effets";
    public const string MusicBus = "Musique";

    private static Sounds? _instance;

    private readonly Dictionary<string, (AudioStream Stream, float VolumeDb)> _sounds = new();
    private readonly List<AudioStreamPlayer> _players = new();
    private int _next;
    private int _generation;
    private int _musicGeneration;
    private AudioStreamPlayer _music = null!;
    private AudioStreamPlayer _jingle = null!;
    private string? _musicKey;
    private Tween? _musicFade;

    private static readonly HashSet<string> JingleKeys = new() { "end.win", "end.lose" };

    public static float EffectsVolume { get; private set; } = 1f;

    public static float MusicVolume { get; private set; } = 1f;

    public static bool Enabled => EffectsVolume > 0f;

    public static bool MusicEnabled => MusicVolume > 0f;

    public override void _EnterTree()
    {
        _instance = this;
    }

    public override void _ExitTree()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public override void _Ready()
    {
        EnsureBuses();
        for (var index = 0; index < Voices; index++)
        {
            var player = new AudioStreamPlayer { Bus = EffectsBus };
            AddChild(player);
            _players.Add(player);
        }

        _music = new AudioStreamPlayer { Bus = MusicBus };
        AddChild(_music);
        _jingle = new AudioStreamPlayer { Bus = MusicBus };
        AddChild(_jingle);
    }

    private static void EnsureBuses()
    {
        foreach (var name in new[] { EffectsBus, MusicBus })
        {
            if (AudioServer.GetBusIndex(name) >= 0)
            {
                continue;
            }

            AudioServer.AddBus();
            var index = AudioServer.BusCount - 1;
            AudioServer.SetBusName(index, name);
            AudioServer.SetBusSend(index, "Master");
        }
    }

    public void Load(IReadOnlyDictionary<string, SoundEntry> table)
    {
        var issues = new List<LoadIssue>();
        _sounds.Clear();
        foreach (var (key, entry) in table)
        {
            var streams = new List<AudioStream>();
            foreach (var file in entry.Files)
            {
                var path = $"{SoundsDirectory}/{file}";
                var stream = ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path) : null;
                if (stream is null)
                {
                    issues.Add(new LoadIssue(TableFile, 0, 0, $"« {key} » : fichier son introuvable ou illisible ({path})."));
                    continue;
                }

                streams.Add(stream);
            }

            if (streams.Count == 0)
            {
                continue;
            }

            var sound = streams.Count == 1 ? streams[0] : Variants(streams);
            if (key.StartsWith("dc.", StringComparison.Ordinal) || key == "mu.title")
            {
                MakeLoop(sound);
            }

            _sounds[key] = (sound, entry.VolumeDb);
        }

        if (issues.Count > 0)
        {
            throw new LevelLoadException(issues);
        }
    }

    public static void SetEffectsVolume(float volume)
    {
        EnsureBuses();
        EffectsVolume = Math.Clamp(volume, 0f, 1f);
        ApplyBusVolume(EffectsBus, EffectsVolume);
        if (!Enabled)
        {
            CancelPending();
        }
    }

    public static void SetMusicVolume(float volume)
    {
        EnsureBuses();
        MusicVolume = Math.Clamp(volume, 0f, 1f);
        ApplyBusVolume(MusicBus, MusicVolume);
    }

    private static void ApplyBusVolume(string bus, float volume)
    {
        var index = AudioServer.GetBusIndex(bus);
        AudioServer.SetBusMute(index, volume <= 0f);
        AudioServer.SetBusVolumeDb(index, volume <= 0f ? SilentDb : Mathf.LinearToDb(volume));
    }

    private static bool IsAudible(string key) => JingleKeys.Contains(key) ? MusicEnabled : Enabled;

    public static void PlayMusic(string key, double delay = 0)
    {
        if (_instance is not { } sounds || !sounds._sounds.TryGetValue(key, out var sound))
        {
            return;
        }

        if (sounds._musicKey == key && sounds._music.Playing && sounds._musicFade is null)
        {
            return;
        }

        sounds.KillMusicFade();
        sounds._music.Stop();
        sounds._musicKey = key;
        var generation = ++sounds._musicGeneration;
        void Start()
        {
            if (generation != sounds._musicGeneration || !IsInstanceValid(sounds))
            {
                return;
            }

            sounds._music.Stream = sound.Stream;
            sounds._music.VolumeDb = sound.VolumeDb;
            sounds._music.Play();
        }

        if (delay <= 0)
        {
            Start();
            return;
        }

        sounds.GetTree().CreateTimer(delay).Timeout += Start;
    }

    public static void StopMusic()
    {
        if (_instance is not { } sounds || sounds._musicKey is null)
        {
            return;
        }

        sounds._musicKey = null;
        sounds._musicGeneration++;
        if (!sounds._music.Playing)
        {
            return;
        }

        sounds.KillMusicFade();
        var fade = sounds.CreateTween();
        fade.TweenProperty(sounds._music, "volume_db", SilentDb, MusicFadeSeconds);
        fade.TweenCallback(Callable.From(() =>
        {
            sounds._music.Stop();
            sounds._musicFade = null;
        }));
        sounds._musicFade = fade;
    }

    private void KillMusicFade()
    {
        _musicFade?.Kill();
        _musicFade = null;
    }

    public static void Play(string key, double delay = 0)
    {
        if (_instance is not { } sounds || !IsAudible(key) || !sounds._sounds.ContainsKey(key))
        {
            return;
        }

        if (delay <= 0)
        {
            sounds.PlayNow(key);
            return;
        }

        var generation = sounds._generation;
        sounds.GetTree().CreateTimer(delay).Timeout += () =>
        {
            if (generation == sounds._generation && IsInstanceValid(sounds))
            {
                sounds.PlayNow(key);
            }
        };
    }

    public static void CancelPending()
    {
        if (_instance is { } sounds)
        {
            sounds._generation++;
        }
    }

    public static (AudioStream Stream, float VolumeDb)? Find(string key) =>
        _instance is { } sounds && sounds._sounds.TryGetValue(key, out var sound) ? sound : null;

    public static string ButtonKey(string text) =>
        text == Texts.BackButton || text == Texts.CancelButton ? "ui.back"
        : text == Texts.ConfirmButton ? "ui.confirm"
        : "ui.tap";

    private void PlayNow(string key)
    {
        if (!IsAudible(key) || !_sounds.TryGetValue(key, out var sound) || _players.Count == 0)
        {
            return;
        }

        if (JingleKeys.Contains(key))
        {
            _jingle.Stream = sound.Stream;
            _jingle.VolumeDb = sound.VolumeDb;
            _jingle.Play();
            return;
        }

        var player = _players.FirstOrDefault(candidate => !candidate.Playing);
        if (player is null)
        {
            player = _players[_next];
            _next = (_next + 1) % _players.Count;
        }

        player.Stream = sound.Stream;
        player.VolumeDb = sound.VolumeDb;
        player.Play();
    }

    private static AudioStream Variants(IReadOnlyList<AudioStream> streams)
    {
        var randomizer = new AudioStreamRandomizer
        {
            PlaybackMode = AudioStreamRandomizer.PlaybackModeEnum.RandomNoRepeats,
            RandomPitch = VariantPitch,
        };
        for (var index = 0; index < streams.Count; index++)
        {
            randomizer.AddStream(index, streams[index]);
        }

        return randomizer;
    }

    private static void MakeLoop(AudioStream stream)
    {
        switch (stream)
        {
            case AudioStreamWav wav:
                wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
                wav.LoopBegin = 0;
                wav.LoopEnd = (int)Math.Round(wav.GetLength() * wav.MixRate);
                break;
            case AudioStreamOggVorbis ogg:
                ogg.Loop = true;
                break;
            default:
                GD.PushWarning($"Son d'ambiance non bouclable : {stream.ResourcePath}.");
                break;
        }
    }
}

public static class TurnSounds
{
    public static void Play(IReadOnlyList<GameEvent> events, double monstersDelay, LevelDefinition? level)
    {
        string Gate(Position position, string suffix) => (level?.IsDoor(position) ?? false ? "dr." : "gr.") + suffix;

        var teleported = events.Any(gameEvent => gameEvent is MagusMoved { Cause: MoveCause.Teleport });
        var magusArrival = teleported ? SpellFx.TeleportHalfDuration : LevelView.MoveDuration;
        var settled = monstersDelay + LevelView.MoveDuration;
        var cues = new HashSet<(string Key, double Delay)>();
        foreach (var gameEvent in events)
        {
            switch (gameEvent)
            {
                case MagusMoved { Cause: MoveCause.Step }:
                    cues.Add(("mg.step", 0));
                    break;
                case MagusMoved { Cause: MoveCause.Teleport }:
                    cues.Add(("sp.magus_out", 0));
                    cues.Add(("sp.magus_in", SpellFx.TeleportHalfDuration));
                    break;
                case CreaturesSwapped:
                    cues.Add(("sp.creatura", 0));
                    break;
                case MonsterStunned:
                    cues.Add(("mo.stun", monstersDelay));
                    break;
                case ContainerBroken broken:
                    cues.Add((level?.TerrainAt(broken.Position) == Terrain.Vase ? "it.break_vase" : "it.break", 0));
                    if (broken.HadPotion)
                    {
                        cues.Add(("it.reveal", LevelView.PotionAppearDelay));
                    }

                    break;
                case PotionPicked:
                    cues.Add(("it.drink", magusArrival));
                    break;
                case MonsterMutationStarted:
                    cues.Add(("mo.mut_on", settled));
                    break;
                case MonsterMutated:
                    cues.Add(("mo.mut_off", settled));
                    break;
                case LeverPulled:
                    cues.Add(("gr.lever", 0));
                    break;
                case AlcoveLit:
                    cues.Add(("it.alcove", 0));
                    break;
                case GrilleOpened opened:
                    cues.Add((Gate(opened.Position, "open"), 0));
                    break;
                case GrilleClosed closed:
                    cues.Add((Gate(closed.Position, "close"), settled));
                    break;
                case GrilleCloseDelayed delayed:
                    cues.Add((Gate(delayed.Position, "delayed"), settled));
                    break;
                case MagusCaptured:
                    cues.Add(("end.caught", settled));
                    break;
                case MagusBitten:
                    cues.Add(("mo.bite", monstersDelay));
                    break;
                case MagusFainted:
                    cues.Add(("mo.faint", monstersDelay));
                    break;
                case LevelCompleted:
                    cues.Add(("end.win", magusArrival));
                    break;
            }
        }

        foreach (var (key, delay) in cues)
        {
            Sounds.Play(key, delay);
        }
    }
}
