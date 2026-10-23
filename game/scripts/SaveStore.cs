using Godot;
using SpacioMagus.Core.Loading;
using SpacioMagus.Core.Saving;

namespace SpacioMagus;

public static class SaveStore
{
    private const string FileName = "sauvegarde.json";

    private static string FilePath => UserData.Path(FileName);

    private static string TempPath => UserData.Path("sauvegarde.json.tmp");

    public static bool Exists => FileAccess.FileExists(FilePath);

    public static SaveGame? Load()
    {
        if (!Exists)
        {
            return null;
        }

        using var file = FileAccess.Open(FilePath, FileAccess.ModeFlags.Read);
        if (file is null)
        {
            GD.PushWarning($"Sauvegarde illisible ({FilePath}) : {FileAccess.GetOpenError()}.");
            return null;
        }

        try
        {
            return SaveCodec.Parse(FileName, file.GetAsText());
        }
        catch (LevelLoadException exception)
        {
            GD.PushWarning($"Sauvegarde ignorée : {exception.Message}");
            return null;
        }
    }

    public static void Write(SaveGame save) => WriteAtomic(FilePath, TempPath, SaveCodec.Serialize(save));

    public static bool WriteAtomic(string path, string tempPath, string content)
    {
        using (var file = FileAccess.Open(tempPath, FileAccess.ModeFlags.Write))
        {
            if (file is null)
            {
                GD.PushWarning($"Écriture impossible ({tempPath}) : {FileAccess.GetOpenError()}.");
                return false;
            }

            file.StoreString(content);
            file.Flush();
            file.Close();
        }

        var source = ProjectSettings.GlobalizePath(tempPath);
        var target = ProjectSettings.GlobalizePath(path);
        var error = DirAccess.RenameAbsolute(source, target);
        if (error != Error.Ok && FileAccess.FileExists(path))
        {
            DirAccess.RemoveAbsolute(target);
            error = DirAccess.RenameAbsolute(source, target);
        }

        if (error != Error.Ok)
        {
            GD.PushWarning($"Fichier non remplacé ({path}) : {error}.");
            return false;
        }

        return true;
    }

    public static void Archive(SaveGame save, string outcome)
    {
        var folder = UserData.Path("parties");
        var error = DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(folder));
        if (error != Error.Ok)
        {
            GD.PushWarning($"Dossier des parties impossible à créer ({folder}) : {error}.");
            return;
        }

        var level = System.IO.Path.GetFileNameWithoutExtension(save.LevelFile);
        var path = $"{folder}/{level}_{System.DateTime.Now:yyyyMMdd-HHmmss}_{outcome}.json";
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file is null)
        {
            GD.PushWarning($"Partie non enregistrée ({path}) : {FileAccess.GetOpenError()}.");
            return;
        }

        file.StoreString(SaveCodec.Serialize(save));
        GD.Print($"Partie enregistrée : {ProjectSettings.GlobalizePath(path)}");
    }

    public static void Delete()
    {
        foreach (var path in new[] { FilePath, TempPath })
        {
            if (FileAccess.FileExists(path))
            {
                var error = DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
                if (error != Error.Ok)
                {
                    GD.PushWarning($"Sauvegarde non supprimée ({path}) : {error}.");
                }
            }
        }
    }
}
