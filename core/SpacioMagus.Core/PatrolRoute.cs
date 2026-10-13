namespace SpacioMagus.Core;

public static class PatrolRoute
{
    public static IReadOnlyList<Position> Expand(IReadOnlyList<Position> waypoints)
    {
        ArgumentNullException.ThrowIfNull(waypoints);
        if (waypoints.Count == 0)
        {
            throw new ArgumentException("Une ronde doit contenir au moins un point.", nameof(waypoints));
        }

        var route = new List<Position> { waypoints[0] };
        if (waypoints.Count == 1)
        {
            return route;
        }

        for (var index = 0; index < waypoints.Count; index++)
        {
            var from = waypoints[index];
            var to = waypoints[(index + 1) % waypoints.Count];
            var current = from;
            while (current != to)
            {
                current += new Position(Math.Sign(to.X - current.X), Math.Sign(to.Y - current.Y));
                if (current != waypoints[0] || index < waypoints.Count - 1)
                {
                    route.Add(current);
                }
            }
        }

        return route;
    }

    public static int Period(IEnumerable<int> lengths)
    {
        var period = 1;
        foreach (var length in lengths)
        {
            period = period / Gcd(period, length) * length;
        }

        return period;
    }

    private static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);
}
