namespace SpacioMagus.Core;

public readonly record struct Position(int X, int Y)
{
    public static readonly IReadOnlyList<Position> Directions = new Position[]
    {
        new(0, -1), new(1, 0), new(0, 1), new(-1, 0),
        new(1, -1), new(1, 1), new(-1, 1), new(-1, -1),
    };

    public static Position operator +(Position a, Position b) => new(a.X + b.X, a.Y + b.Y);

    public static Position operator -(Position a, Position b) => new(a.X - b.X, a.Y - b.Y);

    public int ChebyshevTo(Position other) => Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));

    public bool IsAdjacentTo(Position other) => ChebyshevTo(other) == 1;

    public override string ToString() => $"({X}, {Y})";
}
