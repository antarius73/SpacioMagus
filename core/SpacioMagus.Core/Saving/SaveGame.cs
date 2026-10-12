namespace SpacioMagus.Core.Saving;

public sealed record SaveGame(
    int RulesVersion,
    string LevelFile,
    string? Fingerprint,
    ulong Seed,
    IReadOnlyList<GameAction> Actions,
    string? Checksum)
{
    public static SaveGame LevelStart(string levelFile, ulong seed) =>
        new(Game.RulesVersion, levelFile, null, seed, Array.Empty<GameAction>(), null);

    public static SaveGame InProgress(string levelFile, string fingerprint, GameJournal journal) =>
        new(Game.RulesVersion, levelFile, fingerprint, journal.Seed, journal.Actions.ToArray(), GameDigest.Compute(journal.Game));
}

public sealed record RestoredGame(GameJournal Journal, bool FellBack);

public static class SaveRestorer
{
    public static RestoredGame Restore(SaveGame save, LevelDefinition level, RuleSet rules, string fingerprint)
    {
        if (save.Actions.Count == 0)
        {
            return new RestoredGame(new GameJournal(level, rules, save.Seed), false);
        }

        if (save.RulesVersion != Game.RulesVersion || save.Fingerprint != fingerprint)
        {
            return Restart(save, level, rules);
        }

        var journal = new GameJournal(level, rules, save.Seed);
        foreach (var action in save.Actions)
        {
            if (!journal.Play(action).Success)
            {
                return Restart(save, level, rules);
            }
        }

        if (journal.Game.Status != GameStatus.Playing || GameDigest.Compute(journal.Game) != save.Checksum)
        {
            return Restart(save, level, rules);
        }

        return new RestoredGame(journal, false);
    }

    private static RestoredGame Restart(SaveGame save, LevelDefinition level, RuleSet rules) =>
        new(new GameJournal(level, rules, save.Seed), true);
}
