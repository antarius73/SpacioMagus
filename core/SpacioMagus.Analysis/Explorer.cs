using SpacioMagus.Core;

namespace SpacioMagus.Analysis;

public sealed record PathStep(int Turn, string Action, int LegalActions, int WinningActions, int LosingActions);

public sealed class ExplorationResult
{
    public int States { get; init; }

    public bool Truncated { get; init; }

    public int DeepestLayer { get; init; }

    public int? ShortestWin { get; init; }

    public double ShortestSolutions { get; init; }

    public IReadOnlyList<GameAction> Solution { get; init; } = Array.Empty<GameAction>();

    public IReadOnlyList<PathStep> Steps { get; init; } = Array.Empty<PathStep>();

    public int WinnableStates { get; init; }

    public long Transitions { get; init; }

    public long LosingTransitions { get; init; }

    public int MagicSpent { get; init; }

    public int SpacioMagusCasts { get; init; }

    public int SpacioCreaturaCasts { get; init; }

    public int Breaks { get; init; }

    public bool Solvable => ShortestWin is not null;
}

public static class Explorer
{
    public static ExplorationResult Explore(Game start, IRandomSource random, ActionFilter filter, int maxStates, bool stopAtFirstWin, int maxDepth = int.MaxValue)
    {
        var parents = new List<int>();
        var actions = new List<GameAction?>();
        var depths = new List<int>();
        var ways = new List<double>();
        var children = new List<List<int>>();
        var legal = new List<int>();
        var wins = new List<int>();
        var losses = new List<int>();
        var index = new Dictionary<string, int>();
        var queue = new Queue<(int Id, Game Game)>();

        int Add(int parent, GameAction? action, int depth, double count, string key)
        {
            var id = parents.Count;
            parents.Add(parent);
            actions.Add(action);
            depths.Add(depth);
            ways.Add(count);
            children.Add(new List<int>());
            legal.Add(0);
            wins.Add(0);
            losses.Add(0);
            index[key] = id;
            return id;
        }

        var root = start.Clone(random);
        queue.Enqueue((Add(-1, null, 0, 1, StateKey.Of(root)), root));

        int? shortest = null;
        var winParent = -1;
        GameAction? winAction = null;
        double solutions = 0;
        var truncated = false;
        long transitions = 0;
        long losing = 0;
        var deepest = 0;

        while (queue.Count > 0)
        {
            var (id, game) = queue.Dequeue();
            var depth = depths[id];
            deepest = Math.Max(deepest, depth);
            if (stopAtFirstWin && shortest is not null && depth >= shortest)
            {
                break;
            }

            if (depth >= maxDepth)
            {
                continue;
            }

            foreach (var action in ActionGenerator.Candidates(game, filter))
            {
                var child = game.Clone(random);
                if (!child.Apply(action).Success)
                {
                    continue;
                }

                transitions++;
                legal[id]++;
                if (child.Status == GameStatus.Won)
                {
                    wins[id]++;
                    if (shortest is null)
                    {
                        shortest = depth + 1;
                        winParent = id;
                        winAction = action;
                    }

                    if (depth + 1 == shortest)
                    {
                        solutions += ways[id];
                    }

                    continue;
                }

                if (child.Status == GameStatus.Lost)
                {
                    losses[id]++;
                    losing++;
                    continue;
                }

                var key = StateKey.Of(child);
                if (index.TryGetValue(key, out var known))
                {
                    children[id].Add(known);
                    if (depths[known] == depth + 1)
                    {
                        ways[known] += ways[id];
                    }

                    continue;
                }

                if (parents.Count >= maxStates)
                {
                    truncated = true;
                    continue;
                }

                var created = Add(id, action, depth + 1, ways[id], key);
                children[id].Add(created);
                queue.Enqueue((created, child));
            }
        }

        var winnable = Winnable(children, wins);
        var path = new List<int>();
        var solution = new List<GameAction>();
        if (winParent >= 0 && winAction is not null)
        {
            for (var node = winParent; node >= 0; node = parents[node])
            {
                path.Add(node);
            }

            path.Reverse();
            solution.AddRange(path.Skip(1).Select(node => actions[node]!));
            solution.Add(winAction);
        }

        var steps = new List<PathStep>();
        for (var step = 0; step < path.Count; step++)
        {
            var node = path[step];
            var keeping = wins[node] + children[node].Count(child => winnable[child]);
            steps.Add(new PathStep(step + 1, ActionGenerator.Describe(solution[step]), legal[node], keeping, losses[node]));
        }

        return new ExplorationResult
        {
            States = parents.Count,
            Truncated = truncated,
            DeepestLayer = deepest,
            ShortestWin = shortest,
            ShortestSolutions = solutions,
            Solution = solution,
            Steps = steps,
            WinnableStates = winnable.Count(value => value),
            Transitions = transitions,
            LosingTransitions = losing,
            MagicSpent = solution.Count(action => action is SpacioMagusAction or SpacioCreaturaAction) * start.Rules.SpellCost,
            SpacioMagusCasts = solution.Count(action => action is SpacioMagusAction),
            SpacioCreaturaCasts = solution.Count(action => action is SpacioCreaturaAction),
            Breaks = solution.Count(action => action is BreakAction),
        };
    }

    private static bool[] Winnable(List<List<int>> children, List<int> wins)
    {
        var reverse = new List<int>[children.Count];
        for (var node = 0; node < children.Count; node++)
        {
            reverse[node] = new List<int>();
        }

        for (var node = 0; node < children.Count; node++)
        {
            foreach (var child in children[node])
            {
                reverse[child].Add(node);
            }
        }

        var winnable = new bool[children.Count];
        var queue = new Queue<int>();
        for (var node = 0; node < children.Count; node++)
        {
            if (wins[node] > 0)
            {
                winnable[node] = true;
                queue.Enqueue(node);
            }
        }

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            foreach (var parent in reverse[node])
            {
                if (!winnable[parent])
                {
                    winnable[parent] = true;
                    queue.Enqueue(parent);
                }
            }
        }

        return winnable;
    }
}
