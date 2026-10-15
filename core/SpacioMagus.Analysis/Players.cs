using SpacioMagus.Core;

namespace SpacioMagus.Analysis;

public enum PlayOutcome
{
    Won,
    Lost,
    Stuck,
}

public sealed record PlayResult(PlayOutcome Outcome, int Turns, IReadOnlyList<string> Actions);

public sealed record PlayoutSummary(int Games, int Won, int Lost, int Stuck, double AverageWinTurns, double AverageLossTurn)
{
    public double WinRate => Games == 0 ? 0 : (double)Won / Games;
}

public sealed class Players
{
    private const int MaxTurns = 100;
    private const int MaxRepeats = 4;
    private readonly Dictionary<Position, int> _distances;
    private readonly IRandomSource _random;

    public Players(LevelDefinition level, IRandomSource random)
    {
        _random = random;
        _distances = DistancesToStairs(level);
    }

    public PlayResult Greedy(Game start, bool cautious, bool recordActions)
    {
        var game = start.Clone(_random);
        var seen = new Dictionary<string, int>();
        var played = new List<string>();
        while (game.Status == GameStatus.Playing && game.Turn < MaxTurns)
        {
            var key = StateKey.Of(game);
            seen[key] = seen.GetValueOrDefault(key) + 1;
            if (seen[key] > MaxRepeats)
            {
                return new PlayResult(PlayOutcome.Stuck, game.Turn, played);
            }

            var choice = BestAction(game, cautious);
            if (choice is null)
            {
                return new PlayResult(PlayOutcome.Stuck, game.Turn, played);
            }

            game = choice.Value.Next;
            if (recordActions)
            {
                played.Add(ActionGenerator.Describe(choice.Value.Action));
            }
        }

        return new PlayResult(Outcome(game), game.Turn, played);
    }

    public PlayoutSummary Playouts(Game start, int games, double exploration, int seed)
    {
        var chooser = new Random(seed);
        int won = 0, lost = 0, stuck = 0;
        double winTurns = 0, lossTurns = 0;
        for (var round = 0; round < games; round++)
        {
            var game = start.Clone(_random);
            while (game.Status == GameStatus.Playing && game.Turn < MaxTurns)
            {
                Game? next = null;
                if (chooser.NextDouble() < exploration)
                {
                    next = RandomAction(game, chooser);
                }

                next ??= BestAction(game, true)?.Next;
                if (next is null)
                {
                    break;
                }

                game = next;
            }

            switch (Outcome(game))
            {
                case PlayOutcome.Won:
                    won++;
                    winTurns += game.Turn;
                    break;
                case PlayOutcome.Lost:
                    lost++;
                    lossTurns += game.Turn;
                    break;
                default:
                    stuck++;
                    break;
            }
        }

        return new PlayoutSummary(games, won, lost, stuck, won == 0 ? 0 : winTurns / won, lost == 0 ? 0 : lossTurns / lost);
    }

    private static PlayOutcome Outcome(Game game) => game.Status switch
    {
        GameStatus.Won => PlayOutcome.Won,
        GameStatus.Lost => PlayOutcome.Lost,
        _ => PlayOutcome.Stuck,
    };

    private Game? RandomAction(Game game, Random chooser)
    {
        var actions = ActionGenerator.Candidates(game, ActionFilter.All).ToList();
        while (actions.Count > 0)
        {
            var pick = chooser.Next(actions.Count);
            var next = game.Clone(_random);
            if (next.Apply(actions[pick]).Success)
            {
                return next;
            }

            actions.RemoveAt(pick);
        }

        return null;
    }

    private (GameAction Action, Game Next)? BestAction(Game game, bool cautious)
    {
        (GameAction Action, Game Next, int Score)? best = null;
        foreach (var action in ActionGenerator.Candidates(game, ActionFilter.All))
        {
            var next = game.Clone(_random);
            if (!next.Apply(action).Success)
            {
                continue;
            }

            if (cautious && next.Status == GameStatus.Lost)
            {
                continue;
            }

            var score = next.Status == GameStatus.Won ? int.MinValue : Distance(next.Magus.Position) * 10 + Priority(action);
            if (best is null || score < best.Value.Score)
            {
                best = (action, next, score);
            }
        }

        return best is { } chosen ? (chosen.Action, chosen.Next) : null;
    }

    private int Distance(Position position) => _distances.TryGetValue(position, out var distance) ? distance : 10_000;

    private static int Priority(GameAction action) => action switch
    {
        OpenGrilleAction => 0,
        LeverAction => 0,
        SpacioMagusAction => 1,
        MoveAction => 2,
        BreakAction => 3,
        SpacioCreaturaAction => 4,
        _ => 5,
    };

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
                if (distances.ContainsKey(next) || LevelDefinition.IsSolid(terrain) || LevelDefinition.IsObstacle(terrain))
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
