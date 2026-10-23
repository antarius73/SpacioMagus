using Godot;
using SpacioMagus.Core.Loading;
using SpacioMagus.Core.Progress;

namespace SpacioMagus;

public static class RecordStore
{
    private const string FileName = "records.json";

    private static string FilePath => UserData.Path(FileName);

    private static string TempPath => UserData.Path("records.json.tmp");

    private static string BrokenPath => UserData.Path("records.json.illisible");

    public static RecordBook Load()
    {
        if (!FileAccess.FileExists(FilePath))
        {
            return new RecordBook();
        }

        string content;
        using (var file = FileAccess.Open(FilePath, FileAccess.ModeFlags.Read))
        {
            if (file is null)
            {
                GD.PushWarning($"Records illisibles ({FilePath}) : {FileAccess.GetOpenError()}.");
                return new RecordBook();
            }

            content = file.GetAsText();
        }

        try
        {
            return RecordCodec.Parse(FileName, content);
        }
        catch (LevelLoadException exception)
        {
            GD.PushWarning($"Records ignorés, fichier mis de côté dans {BrokenPath} : {exception.Message}");
            DirAccess.RenameAbsolute(ProjectSettings.GlobalizePath(FilePath), ProjectSettings.GlobalizePath(BrokenPath));
            return new RecordBook();
        }
    }

    public static void Save(RecordBook book) => SaveStore.WriteAtomic(FilePath, TempPath, RecordCodec.Serialize(book));

    public static void Clear(RecordBook book)
    {
        book.Clear();
        Save(book);
    }
}
