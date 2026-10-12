using System.Collections.Generic;
using Godot;
using SpacioMagus.Core;
using SpacioMagus.Core.Loading;

namespace SpacioMagus;

public static class GameData
{
    private const string DataDirectory = "res://data";
    private const string LevelsDirectory = "res://niveaux";

    public static GameSettings LoadSettings() =>
        GameSettings.Parse("regles.txt", Read($"{DataDirectory}/regles.txt"));

    public static Legend LoadLegend() =>
        Legend.Parse("legende.txt", Read($"{DataDirectory}/legende.txt"));

    public static IReadOnlyList<string> LoadLevelList() =>
        LevelList.Parse("niveaux.txt", Read($"{DataDirectory}/niveaux.txt"));

    public static LoadedLevel LoadLevel(string fileName, string content, Legend legend, RuleSet rules) =>
        LevelFile.Parse(fileName, content, legend, rules);

    public static string ReadLevel(string fileName) => Read($"{LevelsDirectory}/{fileName}");

    public static string ReadText(string fileName) => Read($"{DataDirectory}/{fileName}");

    private static string Read(string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file is null)
        {
            throw new System.IO.IOException($"Impossible de lire {path} : {FileAccess.GetOpenError()}.");
        }

        return file.GetAsText();
    }
}
