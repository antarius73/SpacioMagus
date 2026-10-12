namespace SpacioMagus.Core;

public sealed class MagusState
{
    internal MagusState(Position position, int magic)
    {
        Position = position;
        Magic = magic;
    }

    public Position Position { get; internal set; }

    public int Magic { get; internal set; }
}

public sealed class MonsterState
{
    internal MonsterState(int id, MonsterKind kind, Position position, Position post)
    {
        Id = id;
        Kind = kind;
        Position = position;
        Post = post;
    }

    public int Id { get; }

    public MonsterKind Kind { get; internal set; }

    public Position Position { get; internal set; }

    public Position Post { get; }

    public bool IsTracking { get; internal set; }

    public int StunTurns { get; internal set; }

    public bool IsStunned => StunTurns > 0;

    public int FearTurns { get; internal set; }

    public int MutationTurns { get; internal set; }

    public bool IsMutating => MutationTurns > 0;
}

public sealed class GrilleState
{
    internal GrilleState(Position position, Position firstSide, Position secondSide)
    {
        Position = position;
        FirstSide = firstSide;
        SecondSide = secondSide;
    }

    public Position Position { get; }

    public Position FirstSide { get; }

    public Position SecondSide { get; }

    public int TurnsRemaining { get; internal set; }

    public bool IsOpen => TurnsRemaining > 0;

    internal bool OpenedThisTurn { get; set; }
}
