using System.Linq;
using Godot;

namespace SpacioMagus;

public static class UserSettings
{
    private const string FilePath = "user://reglages.cfg";
    private const string Section = "jeu";
    private const string LanguageKey = "langue";
    private const string SoundKey = "son";
    private const string MusicKey = "musique";

    public static string Language { get; private set; } = SystemLanguage();

    public static bool SoundEnabled { get; private set; } = true;

    public static bool MusicEnabled { get; private set; } = true;

    public static void Load()
    {
        var config = new ConfigFile();
        if (config.Load(FilePath) == Error.Ok)
        {
            Language = config.GetValue(Section, LanguageKey, Language).AsString();
            SoundEnabled = config.GetValue(Section, SoundKey, SoundEnabled).AsBool();
            MusicEnabled = config.GetValue(Section, MusicKey, MusicEnabled).AsBool();
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

    public static void SetSoundEnabled(bool enabled)
    {
        SoundEnabled = enabled;
        Save(SoundKey, enabled);
    }

    public static void SetMusicEnabled(bool enabled)
    {
        MusicEnabled = enabled;
        Save(MusicKey, enabled);
    }

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
