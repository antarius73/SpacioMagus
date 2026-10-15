namespace SpacioMagus.Core.Scripting;

public static class LevelScripts
{
    private static readonly Dictionary<string, ILevelScript> Scripts = new ILevelScript[]
    {
        new BainsDeZorgusScript(),
    }.ToDictionary(script => script.Id);

    public static IEnumerable<string> Ids => Scripts.Keys;

    public static ILevelScript? Find(string? id) =>
        id is not null && Scripts.TryGetValue(id, out var script) ? script : null;
}
