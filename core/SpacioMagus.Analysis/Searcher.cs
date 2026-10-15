using SpacioMagus.Core;

namespace SpacioMagus.Analysis;

public sealed record SearchResult(IReadOnlyList<GameAction>? Solution, int States, bool Truncated);

public static class Searcher
{
    public static SearchResult FindSolution(Game start, IRandomSource random, ActionFilter filter, int maxStates)
    {
        var distances = DistancesToStairs(start.Level);
        var range = Math.Max(1, start.Rules.SpellRange);
        int Estimate(Game game) =>
            distances.TryGetValue(game.Magus.Position, out var distance) ? (distance + range - 1) / range : int.MaxValue / 4;

        var parents = new List<int>();
        var actions = new List<GameAction?>();
        var costs = new Dictionary<string, int>();
        var queue = new PriorityQueue<(int Node, Game Game, int Cost), (int Total, int Depth)>();

        var root = start.Clone(random);
        parents.Add(-1);
        actions.Add(null);
        costs[StateKey.Of(root)] = 0;
        queue.Enqueue((0, root, 0), (Estimate(root), 0));
        var truncated = false;

        while (queue.Count > 0)
        {
            var (node, game, cost) = queue.Dequeue();
            if (costs.TryGetValue(StateKey.Of(game), out var best) && best < cost)
            {
                continue;
            }

            foreach (var action in ActionGenerator.Candidates(game, filter))
            {
                var child = game.Clone(random);
                if (!child.Apply(action).Success || child.Status == GameStatus.Lost || child.IsStranded)
                {
                    continue;
                }

                if (child.Status == GameStatus.Won)
                {
                    return new SearchResult(Path(parents, actions, node, action), parents.Count, false);
                }

                var key = StateKey.Of(child);
                var childCost = cost + 1;
                if (costs.TryGetValue(key, out var known) && known <= childCost)
                {
                    continue;
                }

                if (parents.Count >= maxStates)
                {
                    truncated = true;
                    continue;
                }

                costs[key] = childCost;
                parents.Add(node);
                actions.Add(action);
                queue.Enqueue((parents.Count - 1, child, childCost), (childCost + Estimate(child), -childCost));
            }
        }

        return new SearchResult(null, parents.Count, truncated);
    }

    private static List<GameAction> Path(List<int> parents, List<GameAction?> actions, int node, GameAction last)
    {
        var path = new List<GameAction> { last };
        for (var current = node; current > 0; current = parents[current])
        {
            path.Add(actions[current]!);
        }

        path.Reverse();
        return path;
    }

    private static Dictionary<Position, int> DistancesToStairs(LevelDefinition level)
    {
        var distances = new Dictionary<Position, int>();
        var queue = new Queue<Position>();
        for (var y = 0; y < level.Height; y++)
        {
            for (var x = 0; x < level.Width; x++)
            {
                var position = new Position(x, y);
                if (level.TerrainAt(position) == Terrain.Stairs)
                {
                    distances[position] = 0;
                    queue.Enqueue(position);
                }
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var direction in Position.Directions)
            {
                var next = current + direction;
                var terrain = level.TerrainAt(next);
                if (distances.ContainsKey(next) || LevelDefinition.IsSolid(terrain))
                {
                    continue;
                }

                distances[next] = distances[current] + 1;
                queue.Enqueue(next);
            }
        }

        return distances;
    }
}
