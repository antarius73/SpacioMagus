using System.Linq;
using Godot;

namespace SpacioMagus;

public static class UserSettings
{
    private static string FilePath => UserData.Path("reglages.cfg");
    private const string Section = "jeu";
    private const string LanguageKey = "langue";
    private const string SoundKey = "son";
    private const string MusicKey = "musique";
    private const string EffectsVolumeKey = "volume_effets";
    private const string MusicVolumeKey = "volume_musique";

    public const int DefaultVolume = 80;
    public const int MaxVolume = 100;

    public static string Language { get; private set; } = SystemLanguage();

    public static int EffectsVolume { get; private set; } = DefaultVolume;

    public static int MusicVolume { get; private set; } = DefaultVolume;

    public static void Load()
    {
        var config = new ConfigFile();
        if (config.Load(FilePath) == Error.Ok)
        {
            Language = config.GetValue(Section, LanguageKey, Language).AsString();
            var soundWasOn = config.GetValue(Section, SoundKey, true).AsBool();
            var musicWasOn = config.GetValue(Section, MusicKey, true).AsBool();
            EffectsVolume = ClampVolume(config.GetValue(Section, EffectsVolumeKey, soundWasOn ? DefaultVolume : 0).AsInt32());
            MusicVolume = ClampVolume(config.GetValue(Section, MusicVolumeKey, musicWasOn ? DefaultVolume : 0).AsInt32());
        }

        if (!Texts.Languages.Contains(Language))
        {
            Language = Texts.DefaultLanguage;
        }
    }

    public static void SetLanguage(string language)
    {
        Language = language;
        Save(LanguageKey, language);
    }

    public static void SetEffectsVolume(int volume)
    {
        EffectsVolume = ClampVolume(volume);
        Save(EffectsVolumeKey, EffectsVolume);
    }

    public static void SetMusicVolume(int volume)
    {
        MusicVolume = ClampVolume(volume);
        Save(MusicVolumeKey, MusicVolume);
    }

    private static int ClampVolume(int volume) => System.Math.Clamp(volume, 0, MaxVolume);

    private static string SystemLanguage()
    {
        var system = OS.GetLocaleLanguage();
        return Texts.Languages.Contains(system) ? system : "en";
    }

    private static void Save(string key, Variant value)
    {
        var config = new ConfigFile();
        config.Load(FilePath);
        config.SetValue(Section, key, value);
        var error = config.Save(FilePath);
        if (error != Error.Ok)
        {
            GD.PushWarning($"Réglages non enregistrés ({FilePath}) : {error}");
        }
    }
}
