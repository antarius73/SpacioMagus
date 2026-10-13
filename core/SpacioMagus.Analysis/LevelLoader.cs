using SpacioMagus.Core;
using SpacioMagus.Core.Loading;
using SpacioMagus.Core.Progress;

namespace SpacioMagus.Analysis;

public sealed record LoadedAnalysisLevel(string FileName, LevelDefinition Definition, RuleSet Rules, StarThresholds? Stars);

public static class LevelLoader
{
    public static LoadedAnalysisLevel Load(string levelPath, string? dataDirectory)
    {
        var fullPath = Path.GetFullPath(levelPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Niveau introuvable : {fullPath}");
        }

        var data = dataDirectory ?? Path.Combine(Path.GetDirectoryName(fullPath) ?? ".", "..", "data");
        var rulesPath = Path.Combine(data, "regles.txt");
        var legendPath = Path.Combine(data, "legende.txt");
        foreach (var path in new[] { rulesPath, legendPath })
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Fichier de données introuvable : {Path.GetFullPath(path)} (option --donnees pour indiquer le dossier).");
            }
        }

        var settings = GameSettings.Parse("regles.txt", File.ReadAllText(rulesPath));
        var legend = Legend.Parse("legende.txt", File.ReadAllText(legendPath));
        var fileName = Path.GetFileName(fullPath);
        var level = LevelFile.Parse(fileName, File.ReadAllText(fullPath), legend, settings.Rules);
        return new LoadedAnalysisLevel(fileName, level.Definition, level.Rules, level.Stars);
    }
}
