namespace SpacioMagus.Core.Progress;

public sealed record LevelResult(int Turns, int Stars, int Teleports, int Swaps);

public sealed record LevelRecord(
    int FewestTurns,
    int MostStars,
    int FewestTeleports,
    int MostTeleports,
    int FewestSwaps,
    int MostSwaps)
{
    public static LevelRecord From(LevelResult result) =>
        new(result.Turns, result.Stars, result.Teleports, result.Teleports, result.Swaps, result.Swaps);
}

public sealed record RecordUpdate(
    bool FirstCompletion,
    bool FewestTurns,
    bool MostStars,
    bool FewestTeleports,
    bool MostTeleports,
    bool FewestSwaps,
    bool MostSwaps,
    LevelRecord? Previous,
    LevelRecord Current)
{
    public bool AnyRecord => FirstCompletion || FewestTurns || MostStars || FewestTeleports || MostTeleports || FewestSwaps || MostSwaps;
}

public sealed class RecordBook
{
    private readonly Dictionary<string, LevelRecord> _records;

    public RecordBook()
        : this(new Dictionary<string, LevelRecord>())
    {
    }

    internal RecordBook(Dictionary<string, LevelRecord> records)
    {
        _records = records;
    }

    public IReadOnlyDictionary<string, LevelRecord> Levels => _records;

    public LevelRecord? Get(string levelFile) => _records.TryGetValue(levelFile, out var record) ? record : null;

    public bool IsCompleted(string levelFile) => _records.ContainsKey(levelFile);

    public RecordUpdate Submit(string levelFile, LevelResult result)
    {
        ArgumentNullException.ThrowIfNull(levelFile);
        ArgumentNullException.ThrowIfNull(result);
        if (!_records.TryGetValue(levelFile, out var previous))
        {
            var created = LevelRecord.From(result);
            _records[levelFile] = created;
            return new RecordUpdate(true, true, true, true, true, true, true, null, created);
        }

        var current = new LevelRecord(
            Math.Min(previous.FewestTurns, result.Turns),
            Math.Max(previous.MostStars, result.Stars),
            Math.Min(previous.FewestTeleports, result.Teleports),
            Math.Max(previous.MostTeleports, result.Teleports),
            Math.Min(previous.FewestSwaps, result.Swaps),
            Math.Max(previous.MostSwaps, result.Swaps));
        _records[levelFile] = current;
        return new RecordUpdate(
            false,
            result.Turns < previous.FewestTurns,
            result.Stars > previous.MostStars,
            result.Teleports < previous.FewestTeleports,
            result.Teleports > previous.MostTeleports,
            result.Swaps < previous.FewestSwaps,
            result.Swaps > previous.MostSwaps,
            previous,
            current);
    }

    public void Clear() => _records.Clear();
}
