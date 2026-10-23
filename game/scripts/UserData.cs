using Godot;

namespace SpacioMagus;

public static class UserData
{
    private const string DevFolder = "user://dev";

    private static bool _devFolderReady;

    public static string Path(string name)
    {
        if (!OS.IsDebugBuild())
        {
            return $"user://{name}";
        }

        if (!_devFolderReady)
        {
            var error = DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(DevFolder));
            if (error != Error.Ok)
            {
                GD.PushWarning($"Dossier des données de développement impossible à créer ({DevFolder}) : {error}.");
            }

            _devFolderReady = true;
        }

        return $"{DevFolder}/{name}";
    }
}
