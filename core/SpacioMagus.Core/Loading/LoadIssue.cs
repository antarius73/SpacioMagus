namespace SpacioMagus.Core.Loading;

public sealed record LoadIssue(string File, int Line, int Column, string Message)
{
    public override string ToString()
    {
        if (Line <= 0)
        {
            return $"{File} : {Message}";
        }

        return Column <= 0 ? $"{File}, ligne {Line} : {Message}" : $"{File}, ligne {Line}, colonne {Column} : {Message}";
    }
}

public sealed class LevelLoadException : Exception
{
    public LevelLoadException(IReadOnlyList<LoadIssue> issues)
        : base(string.Join(Environment.NewLine, issues))
    {
        Issues = issues;
    }

    public IReadOnlyList<LoadIssue> Issues { get; }
}
