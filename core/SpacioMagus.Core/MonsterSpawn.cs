namespace SpacioMagus.Core;

public sealed record MonsterSpawn(MonsterKind Kind, Position Start, Position Post, IReadOnlyList<Position>? Route = null)
{
    public IReadOnlyList<Position> Patrol => Route is { Count: > 0 } ? Route : new[] { Post };
}
