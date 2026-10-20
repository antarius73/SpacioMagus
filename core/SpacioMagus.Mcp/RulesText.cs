using System.Text;
using ModelContextProtocol;
using SpacioMagus.Core.Loading;
using SpacioMagus.Core.Remote;

namespace SpacioMagus.Mcp;

public static class RulesText
{
    private static readonly (string Title, string Text)[] Sections =
    {
        ("histoire_titre", "histoire"),
        ("spacio_magus_titre", "spacio_magus"),
        ("spacio_creatura_titre", "spacio_creatura"),
        ("escalier_titre", "escalier"),
        ("fiole_titre", "fiole"),
        ("porte_titre", "porte"),
        ("grille_titre", "grille"),
        ("manette_titre", "manette"),
        ("contenant_titre", "contenant"),
        ("alcove_titre", "alcove"),
        ("blob_vert_titre", "blob_vert"),
        ("blob_rouge_titre", "blob_rouge"),
        ("chauve_souris_titre", "chauve_souris"),
        ("torche_titre", "torche"),
        ("brasero_titre", "brasero"),
        ("eau_titre", "eau"),
        ("coffre_titre", "coffre"),
        ("cle_titre", "cle"),
        ("porte_verrouillee_titre", "porte_verrouillee"),
        ("piege_titre", "piege"),
        ("parchemin_titre", "parchemin"),
    };

    public static string Build(string projectPath)
    {
        var data = Path.Combine(projectPath, "data");
        IReadOnlyDictionary<string, string> texts;
        string settings;
        try
        {
            texts = TextFile.Parse("textes_fr.txt", File.ReadAllText(Path.Combine(data, "textes_fr.txt")));
            settings = File.ReadAllText(Path.Combine(data, "regles.txt"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or LevelLoadException)
        {
            throw new McpException($"Règles illisibles dans « {data} » : {exception.Message}");
        }

        var text = new StringBuilder();
        text.AppendLine("# Mob le Magus — règles vues par le joueur");
        text.AppendLine();
        text.AppendLine("Jeu au tour par tour sur une grille. Chaque action du magus (marcher, attendre, ouvrir, briser, actionner, lancer un sort) prend un tour, puis toutes les créatures jouent.");
        text.AppendLine("But : atteindre l'escalier de sortie (>) sans être capturé. Moins de tours = plus d'étoiles.");
        text.AppendLine("Déplacement : une case voisine, diagonales comprises, sans couper un coin de mur.");
        text.AppendLine();
        foreach (var (title, body) in Sections)
        {
            if (texts.TryGetValue(body, out var content))
            {
                text.Append("## ").AppendLine(texts.TryGetValue(title, out var heading) ? heading.Trim() : body);
                text.AppendLine(content.Trim());
                text.AppendLine();
            }
        }

        text.AppendLine("## Valeurs de base (un niveau peut les modifier)");
        foreach (var line in settings.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith(';')))
        {
            text.Append("- ").AppendLine(line);
        }

        text.AppendLine();
        text.AppendLine("## Légende de la carte");
        text.AppendLine(MapText.Legend);
        return text.ToString();
    }
}
