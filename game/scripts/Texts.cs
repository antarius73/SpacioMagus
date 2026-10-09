using System;
using System.Collections.Generic;
using System.Text;
using Godot;

namespace SpacioMagus;

public static class Texts
{
    public const string DefaultLanguage = "fr";

    public static readonly IReadOnlyList<string> Languages = new[] { "fr", "en" };

    private const string NameKey = "langue";

    private static Dictionary<string, string> _fallback = new();
    private static Dictionary<string, string> _current = new();

    public static string Language { get; private set; } = DefaultLanguage;

    public static void Load(string language)
    {
        _fallback = Parse(DefaultLanguage, GameData.ReadText(FileName(DefaultLanguage)));
        _current = language == DefaultLanguage ? _fallback : Parse(language, GameData.ReadText(FileName(language)));
        Language = language;
    }

    public static string LanguageName(string language)
    {
        var texts = language == Language ? _current : Parse(language, GameData.ReadText(FileName(language)));
        return texts.TryGetValue(NameKey, out var name) ? name : language;
    }

    public static string LevelStart(string levelName) => string.Format(Get("debut_niveau"), levelName);

    public static string StoryTitle => Get("histoire_titre");

    public static string Story => Get("histoire");

    public static string SpacioMagusTitle => Get("spacio_magus_titre");

    public static string SpacioMagus => Get("spacio_magus");

    public static string SpacioCreaturaTitle => Get("spacio_creatura_titre");

    public static string SpacioCreatura => Get("spacio_creatura");

    public static string VictoryTitle => Get("victoire_titre");

    public static string Victory => Get("victoire");

    public static string DefeatTitle => Get("defaite_titre");

    public static string Defeat => Get("defaite");

    public static string PauseTitle => Get("menu_titre");

    public static string CreditsTitle => Get("credits_titre");

    public static string Credits => Get("credits");

    public static string ErrorTitle => Get("erreur_titre");

    public static string NextButton => Get("bouton_suivant");

    public static string SkipButton => Get("bouton_passer");

    public static string ResumeButton => Get("bouton_reprendre");

    public static string RestartButton => Get("bouton_recommencer");

    public static string PlayAgainButton => Get("bouton_rejouer");

    public static string NextLevelButton => Get("bouton_niveau_suivant");

    public static string CreditsButton => Get("bouton_credits");

    public static string DiscoveryButton => Get("bouton_compris");

    public static string HideTips => Get("astuces_masquer");

    public static string TipsOn => Get("astuces_oui");

    public static string TipsOff => Get("astuces_non");

    public static string LanguageLabel => Get("langue_choix");

    public static string DoorTitle => Get("porte_titre");

    public static string Door => Get("porte");

    public static string PotionTitle => Get("fiole_titre");

    public static string Potion => Get("fiole");

    public static string GreenBlobTitle => Get("blob_vert_titre");

    public static string GreenBlob => Get("blob_vert");

    private static string FileName(string language) => $"textes_{language}.txt";

    private static string Get(string key)
    {
        if (_current.TryGetValue(key, out var text) || _fallback.TryGetValue(key, out text))
        {
            return text;
        }

        GD.PushWarning($"Texte manquant : [{key}] ({Language}).");
        return $"[{key}]";
    }

    private static Dictionary<string, string> Parse(string language, string content)
    {
        var texts = new Dictionary<string, string>();
        string? key = null;
        var buffer = new StringBuilder();
        var lines = content.Replace("\r\n", "\n").Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']') && trimmed.Length > 2)
            {
                Store(texts, key, buffer, language);
                key = trimmed[1..^1].Trim();
                buffer.Clear();
                continue;
            }

            if (key is null)
            {
                if (trimmed.Length > 0 && !trimmed.StartsWith('#'))
                {
                    throw new System.IO.IOException($"textes_{language}.txt ligne {index + 1} : texte hors d'une clé [cle].");
                }

                continue;
            }

            buffer.Append(line.TrimEnd()).Append('\n');
        }

        Store(texts, key, buffer, language);
        return texts;
    }

    private static void Store(Dictionary<string, string> texts, string? key, StringBuilder buffer, string language)
    {
        if (key is null)
        {
            return;
        }

        if (!texts.TryAdd(key, buffer.ToString().Trim('\n')))
        {
            throw new System.IO.IOException($"textes_{language}.txt : clé [{key}] en double.");
        }
    }
}
