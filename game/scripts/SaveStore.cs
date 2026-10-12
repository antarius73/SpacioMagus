using Godot;
using SpacioMagus.Core.Loading;
using SpacioMagus.Core.Saving;

namespace SpacioMagus;

public static class SaveStore
{
    private const string FilePath = "user://sauvegarde.json";
    private const string TempPath = "user://sauvegarde.json.tmp";
    private const string FileName = "sauvegarde.json";

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

    public static void Write(SaveGame save)
    {
        var content = SaveCodec.Serialize(save);
        using (var file = FileAccess.Open(TempPath, FileAccess.ModeFlags.Write))
        {
            if (file is null)
            {
                GD.PushWarning($"Sauvegarde impossible ({TempPath}) : {FileAccess.GetOpenError()}.");
                return;
            }

            file.StoreString(content);
            file.Flush();
            file.Close();
        }

        var source = ProjectSettings.GlobalizePath(TempPath);
        var target = ProjectSettings.GlobalizePath(FilePath);
        var error = DirAccess.RenameAbsolute(source, target);
        if (error != Error.Ok && FileAccess.FileExists(FilePath))
        {
            DirAccess.RemoveAbsolute(target);
            error = DirAccess.RenameAbsolute(source, target);
        }

        if (error != Error.Ok)
        {
            GD.PushWarning($"Sauvegarde non remplacée ({FilePath}) : {error}.");
        }
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
