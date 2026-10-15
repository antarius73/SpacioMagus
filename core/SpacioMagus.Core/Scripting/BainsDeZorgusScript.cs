using System.Runtime.CompilerServices;

namespace SpacioMagus.Core.Scripting;

public sealed class BainsDeZorgusScript : ILevelScript
{
    private const int StreamRow = 17;
    private const int MiddleStreamRow = 10;

    private static readonly Share[][] Shares =
    {
        new[]
        {
            new Share("au nord du cours d'eau", cell => cell.Y < StreamRow, 1),
            new Share("au sud du cours d'eau", cell => cell.Y > StreamRow, 1),
        },
        new[]
        {
            new Share("au nord du cours d'eau", cell => cell.Y < MiddleStreamRow, 1),
            new Share("au sud du cours d'eau", cell => cell.Y > MiddleStreamRow, 1),
        },
        new[] { new Share("", _ => true, 1) },
    };
    private static readonly ConditionalWeakTable<LevelDefinition, Dictionary<Position, Position[]>> LeverChains = new();

    private static readonly Position[] Neighbors = { new(0, -1), new(1, 0), new(0, 1), new(-1, 0) };

    public string Id => "bains_de_zorgus";

    public IEnumerable<string> Validate(LevelDefinition level)
    {
        if (!level.CellsOf(Terrain.Stairs).Any())
        {
            yield return $"Le script « {Id} » ordonne les zones depuis l'escalier de sortie, la carte n'en a pas.";
            yield break;
        }

        var zones = ContainerZones(level);
        if (zones.Count != Shares.Length)
        {
            yield return $"Le script « {Id} » attend {Shares.Length} zones séparées par des herses avec des contenants, la carte en a {zones.Count}.";
            yield break;
        }

        for (var index = 0; index < zones.Count; index++)
        {
            foreach (var share in Shares[index])
            {
                var available = zones[index].Count(share.Accepts);
                if (available < share.Potions)
                {
                    var where = share.Label.Length == 0 ? "" : $" {share.Label}";
                    yield return $"Le script « {Id} » place {share.Potions} fioles{where} dans la zone {index + 1}, qui n'y a que {available} contenants.";
                }
            }
        }

        if (level.Alcoves.Count == 0)
        {
            yield return $"Le script « {Id} » attend une alcôve dans la couche de décor.";
        }
    }

    public void Setup(Game game, IRandomSource random)
    {
        var zones = ContainerZones(game.Level);
        foreach (var container in zones.SelectMany(zone => zone))
        {
            game.SetContainerPotion(container, false);
        }

        for (var index = 0; index < zones.Count && index < Shares.Length; index++)
        {
            foreach (var share in Shares[index])
            {
                var pool = zones[index].Where(share.Accepts).ToList();
                for (var drawn = 0; drawn < share.Potions && pool.Count > 0; drawn++)
                {
                    var pick = random.Next(pool.Count);
                    game.SetContainerPotion(pool[pick], true);
                    pool.RemoveAt(pick);
                }
            }
        }
    }

    public IEnumerable<Position> Activables(Game game) => game.Level.Alcoves;

    public ActionError CanActivate(Game game, Position target)
    {
        if (!game.Level.Alcoves.Contains(target))
        {
            return ActionError.NotActivable;
        }

        if (game.IsActivated(target))
        {
            return ActionError.AlreadyActive;
        }

        return game.Magus.Position.IsAdjacentTo(target) ? ActionError.None : ActionError.NotAdjacent;
    }

    public void Activate(Game game, Position target)
    {
        game.MarkActivated(target);
        game.Emit(new AlcoveLit(target));
        game.RevealPotions();
    }

    public IEnumerable<Position> LeverGrilles(LevelDefinition level, Position lever) =>
        LeverChains.GetValue(level, BuildLeverChains).TryGetValue(lever, out var grilles) ? grilles : Array.Empty<Position>();

    private static Dictionary<Position, Position[]> BuildLeverChains(LevelDefinition level)
    {
        var rooms = Rooms(level);
        int RoomOf(Position cell) => rooms.FindIndex(room => room.Contains(cell));

        var levers = level.Levers.Select(pair => (Lever: pair.Key, Grille: pair.Value, Room: RoomOf(pair.Key))).ToList();
        return levers.ToDictionary(
            entry => entry.Lever,
            entry => new[] { entry.Grille }
                .Concat(levers.Where(other => other.Lever != entry.Lever && other.Room >= 0 && other.Room <= entry.Room).Select(other => other.Grille))
                .Distinct()
                .ToArray());
    }

    private static List<List<Position>> ContainerZones(LevelDefinition level) =>
        Rooms(level)
            .Select(room => room.Where(cell => LevelDefinition.IsContainer(level.TerrainAt(cell))).OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToList())
            .Where(containers => containers.Count > 0)
            .ToList();

    private static List<HashSet<Position>> Rooms(LevelDefinition level)
    {
        var seen = new HashSet<Position>();
        var rooms = new List<HashSet<Position>>();
        for (var y = 0; y < level.Height; y++)
        {
            for (var x = 0; x < level.Width; x++)
            {
                var start = new Position(x, y);
                if (seen.Contains(start) || !IsRoom(level, start))
                {
                    continue;
                }

                var room = new HashSet<Position> { start };
                var queue = new Queue<Position>();
                queue.Enqueue(start);
                seen.Add(start);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    foreach (var direction in Neighbors)
                    {
                        var next = current + direction;
                        if (!seen.Contains(next) && IsRoom(level, next))
                        {
                            seen.Add(next);
                            room.Add(next);
                            queue.Enqueue(next);
                        }
                    }
                }

                if (room.Any(cell => LevelDefinition.IsContainer(level.TerrainAt(cell))))
                {
                    rooms.Add(room);
                }
            }
        }

        var distances = DistancesToExit(level);
        return rooms
            .OrderByDescending(room => room.Min(cell => distances.TryGetValue(cell, out var distance) ? distance : int.MaxValue))
            .ToList();
    }

    private static Dictionary<Position, int> DistancesToExit(LevelDefinition level)
    {
        var distances = new Dictionary<Position, int>();
        var queue = new Queue<Position>();
        foreach (var exit in level.CellsOf(Terrain.Stairs))
        {
            distances[exit] = 0;
            queue.Enqueue(exit);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var direction in Neighbors)
            {
                var next = current + direction;
                if (!distances.ContainsKey(next)
                    && level.InBounds(next)
                    && !LevelDefinition.IsSolid(level.TerrainAt(next)))
                {
                    distances[next] = distances[current] + 1;
                    queue.Enqueue(next);
                }
            }
        }

        return distances;
    }

    private sealed record Share(string Label, Func<Position, bool> Accepts, int Potions);

    private static bool IsRoom(LevelDefinition level, Position position) =>
        level.InBounds(position)
        && !LevelDefinition.IsSolid(level.TerrainAt(position))
        && level.TerrainAt(position) != Terrain.Grille;
}
