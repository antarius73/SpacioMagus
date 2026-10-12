using System.Collections.Generic;
using Godot;
using SpacioMagus.Core.Loading;

namespace SpacioMagus;

public static class Texts
{
    public const string DefaultLanguage = "fr";

    public static readonly IReadOnlyList<string> Languages = new[] { "fr", "en" };

    private const string NameKey = "langue";

    private static IReadOnlyDictionary<string, string> _fallback = new Dictionary<string, string>();
    private static IReadOnlyDictionary<string, string> _current = new Dictionary<string, string>();

    public static string Language { get; private set; } = DefaultLanguage;

    public static void Load(string language)
    {
        var fallback = Parse(DefaultLanguage);
        var current = language == DefaultLanguage ? fallback : Parse(language);
        _fallback = fallback;
        _current = current;
        Language = language;
    }

    public static string LanguageName(string language)
    {
        var texts = language == Language ? _current : Parse(language);
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

    public static string LanguageLabel => Get("langue_choix");

    public static string GameTitle => Get("jeu_titre");

    public static string NewGameButton => Get("bouton_nouvelle_partie");

    public static string ContinueButton => Get("bouton_continuer");

    public static string SettingsButton => Get("bouton_reglages");

    public static string BackButton => Get("bouton_retour");

    public static string MainMenuButton => Get("bouton_menu_principal");

    public static string SettingsTitle => Get("reglages_titre");

    public static string ConfirmNewGameTitle => Get("confirmation_titre");

    public static string ConfirmNewGame => Get("confirmation");

    public static string ConfirmButton => Get("bouton_confirmer");

    public static string CancelButton => Get("bouton_annuler");

    public static string DoorTitle => Get("porte_titre");

    public static string Door => Get("porte");

    public static string PotionTitle => Get("fiole_titre");

    public static string Potion => Get("fiole");

    public static string GreenBlobTitle => Get("blob_vert_titre");

    public static string GreenBlob => Get("blob_vert");

    public static string RedBlobTitle => Get("blob_rouge_titre");

    public static string RedBlob => Get("blob_rouge");

    public static string TorchTitle => Get("torche_titre");

    public static string Torch => Get("torche");

    public static string ContainerTitle => Get("contenant_titre");

    public static string Container => Get("contenant");

    public static string BrazierTitle => Get("brasero_titre");

    public static string Brazier => Get("brasero");

    public static string StairsTitle => Get("escalier_titre");

    public static string Stairs => Get("escalier");

    public static string GestureTitle => Get("geste_titre");

    public static string Gesture => Get("geste");

    public static string CloseHint => Get("fermer_indication");

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

    private static IReadOnlyDictionary<string, string> Parse(string language) =>
        TextFile.Parse(FileName(language), GameData.ReadText(FileName(language)));
}
