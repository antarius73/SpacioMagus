using SpacioMagus.Core.Scripting;

namespace SpacioMagus.Core;

public sealed class Game
{
    private readonly List<MonsterState> _monsters;
    private readonly Dictionary<Position, GrilleState> _grilles;
    private readonly HashSet<Position> _potions;
    private readonly Dictionary<Position, bool> _containers;
    private readonly IRandomSource _random;
    private readonly HashSet<Position> _fireZone;
    private readonly HashSet<Position> _waterZone;
    private readonly HashSet<Position> _activated = new();
    private List<GameEvent> _events = new();

    public const int RulesVersion = 8;
    private const int BatSatedTurns = 2;

    public Game(LevelDefinition level, RuleSet? rules = null, IRandomSource? random = null)
    {
        Level = level ?? throw new ArgumentNullException(nameof(level));
        Rules = rules ?? RuleSet.Default;
        Rules.Validate();
        _random = random ?? new SystemRandomSource();
        Magus = new MagusState(level.MagusStart, Rules.MagicMax);
        _monsters = level.Monsters
            .Select((spawn, index) => new MonsterState(
                index + 1,
                spawn.Kind,
                spawn.Start,
                spawn.Post,
                spawn.Patrol,
                Math.Max(0, spawn.Patrol.ToList().IndexOf(spawn.Start))))
            .ToList();
        _grilles = new Dictionary<Position, GrilleState>();
        foreach (var position in level.Grilles())
        {
            var (first, second) = level.GrilleSides(position);
            _grilles[position] = new GrilleState(position, first, second);
        }

        _potions = new HashSet<Position>(level.Potions.Where(potion => !LevelDefinition.IsContainer(level.TerrainAt(potion))));
        _containers = new Dictionary<Position, bool>();
        for (var y = 0; y < level.Height; y++)
        {
            for (var x = 0; x < level.Width; x++)
            {
                var position = new Position(x, y);
                if (LevelDefinition.IsContainer(level.TerrainAt(position)))
                {
                    _containers[position] = level.Potions.Contains(position);
                }
            }
        }

        _fireZone = BuildFireZone(level);
        _waterZone = BuildWaterZone(level);
        Script = LevelScripts.Find(level.Script);
        Script?.Setup(this, _random);
    }

    private Game(Game source, IRandomSource random)
    {
        Level = source.Level;
        Rules = source.Rules;
        _random = random;
        Status = source.Status;
        LossReason = source.LossReason;
        IsStranded = source.IsStranded;
        Turn = source.Turn;
        Magus = new MagusState(source.Magus.Position, source.Magus.Magic);
        _monsters = source._monsters.Select(monster => new MonsterState(monster.Id, monster.Kind, monster.Position, monster.Post, monster.Route, monster.RouteOffset)
        {
            IsTracking = monster.IsTracking,
            StunTurns = monster.StunTurns,
            FearTurns = monster.FearTurns,
            MutationTurns = monster.MutationTurns,
        }).ToList();
        _grilles = source._grilles.ToDictionary(
            entry => entry.Key,
            entry => new GrilleState(entry.Value.Position, entry.Value.FirstSide, entry.Value.SecondSide)
            {
                TurnsRemaining = entry.Value.TurnsRemaining,
                OpenedThisTurn = entry.Value.OpenedThisTurn,
            });
        _potions = new HashSet<Position>(source._potions);
        _containers = new Dictionary<Position, bool>(source._containers);
        _fireZone = source._fireZone;
        _waterZone = source._waterZone;
        _activated = new HashSet<Position>(source._activated);
        PotionsAreRevealed = source.PotionsAreRevealed;
        Script = source.Script;
    }

    public LevelDefinition Level { get; }

    public int PatrolPeriod => PatrolRoute.Period(_monsters.Select(monster => monster.Route.Count));

    public Game Clone(IRandomSource random) => new(this, random ?? throw new ArgumentNullException(nameof(random)));

    public RuleSet Rules { get; }

    public GameStatus Status { get; private set; } = GameStatus.Playing;

    public LossReason LossReason { get; private set; }

    public bool IsStranded { get; private set; }

    public ILevelScript? Script { get; }

    public bool PotionsAreRevealed { get; private set; }

    public IReadOnlyCollection<Position> ActivatedCells => _activated;

    public bool IsActivated(Position position) => _activated.Contains(position);

    public bool ContainerHasPotion(Position position) => _containers.TryGetValue(position, out var potion) && potion;

    public IEnumerable<Position> Activables() =>
        Script is { } script && Status == GameStatus.Playing
            ? script.Activables(this).Where(cell => script.CanActivate(this, cell) == ActionError.None)
            : Enumerable.Empty<Position>();

    internal void SetContainerPotion(Position position, bool potion)
    {
        if (_containers.ContainsKey(position))
        {
            _containers[position] = potion;
        }
    }

    internal void MarkActivated(Position position) => _activated.Add(position);

    internal void Emit(GameEvent gameEvent) => _events.Add(gameEvent);

    internal void RevealPotions()
    {
        if (PotionsAreRevealed)
        {
            return;
        }

        PotionsAreRevealed = true;
        _events.Add(new PotionsRevealed());
    }

    public int Turn { get; private set; }

    public MagusState Magus { get; }

    public IReadOnlyList<MonsterState> Monsters => _monsters;

    public IReadOnlyCollection<GrilleState> Grilles => _grilles.Values;

    public IReadOnlyCollection<Position> Potions => _potions;

    public IReadOnlyCollection<Position> Containers => _containers.Keys;

    public bool IsContainer(Position position) => _containers.ContainsKey(position);

    public GrilleState? GrilleAt(Position position) =>
        _grilles.TryGetValue(position, out var grille) ? grille : null;

    public MonsterState? MonsterAt(Position position) =>
        _monsters.FirstOrDefault(monster => monster.Position == position);

    public bool IsOccupied(Position position) =>
        Magus.Position == position || MonsterAt(position) is not null;

    public bool BlocksSight(Position position) => Level.TerrainAt(position) switch
    {
        Terrain.Wall or Terrain.Void => true,
        Terrain.Grille => !(GrilleAt(position)?.IsOpen ?? false),
        _ => false,
    };

    public bool IsWalkable(Position position) =>
        !BlocksSight(position) && !IsObstacleAt(position);

    private bool IsObstacleAt(Position position) =>
        LevelDefinition.IsObstacle(Level.TerrainAt(position)) || _containers.ContainsKey(position);

    public IReadOnlyCollection<Position> FearZone(MonsterKind kind) => kind switch
    {
        MonsterKind.BlobVert => _fireZone,
        MonsterKind.BlobRouge => _waterZone,
        _ => Array.Empty<Position>(),
    };

    public bool IsFeared(MonsterKind kind, Position position) => kind switch
    {
        MonsterKind.BlobVert => _fireZone.Contains(position),
        MonsterKind.BlobRouge => _waterZone.Contains(position),
        _ => false,
    };

    public bool IsCornerFree(Position from, Position to)
    {
        var delta = to - from;
        if (delta.X == 0 || delta.Y == 0)
        {
            return true;
        }

        return !BlocksSight(new Position(to.X, from.Y)) && !BlocksSight(new Position(from.X, to.Y));
    }

    public bool HasLineOfSight(Position from, Position to) => ClearLine(from, to);

    public IReadOnlyList<Position> TeleportTargets()
    {
        var targets = new List<Position>();
        if (Status != GameStatus.Playing)
        {
            return targets;
        }

        var range = Rules.SpellRange;
        var origin = Magus.Position;
        for (var y = origin.Y - range; y <= origin.Y + range; y++)
        {
            for (var x = origin.X - range; x <= origin.X + range; x++)
            {
                var cell = new Position(x, y);
                if (CheckTeleport(cell) == ActionError.None)
                {
                    targets.Add(cell);
                }
            }
        }

        return targets;
    }

    public IReadOnlyList<MonsterState> SwapCandidates()
    {
        if (Status != GameStatus.Playing)
        {
            return Array.Empty<MonsterState>();
        }

        return _monsters.Where(monster => CheckSwapTarget(monster) == ActionError.None).ToList();
    }

    public ActionResult Apply(GameAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return Status == GameStatus.Playing && IsStranded ? AnnounceStranded() : Dispatch(action);
    }

    private ActionResult Dispatch(GameAction action) => action switch
    {
        MoveAction move => Move(move.Target),
        WaitAction => Wait(),
        OpenGrilleAction grille => OpenGrille(grille.Target),
        SpacioMagusAction teleport => CastSpacioMagus(teleport.Target),
        SpacioCreaturaAction swap => CastSpacioCreatura(swap.First, swap.Second),
        BreakAction breaking => Break(breaking.Target),
        LeverAction lever => PullLever(lever.Target),
        ActivateAction activate => Activate(activate.Target),
        _ => throw new ArgumentException($"Action inconnue : {action.GetType().Name}.", nameof(action)),
    };

    public ActionResult Move(Position target)
    {
        if (Status != GameStatus.Playing)
        {
            return ActionResult.Fail(ActionError.GameOver);
        }

        if (!Magus.Position.IsAdjacentTo(target))
        {
            return ActionResult.Fail(ActionError.NotAdjacent);
        }

        if (!IsWalkable(target))
        {
            return ActionResult.Fail(ActionError.Blocked);
        }

        if (MonsterAt(target) is not null)
        {
            return ActionResult.Fail(ActionError.OccupiedByMonster);
        }

        if (!IsCornerFree(Magus.Position, target))
        {
            return ActionResult.Fail(ActionError.CornerBlocked);
        }

        BeginTurn();
        MoveMagus(target, MoveCause.Step);
        return EndTurn();
    }

    public ActionResult Wait()
    {
        if (Status != GameStatus.Playing)
        {
            return ActionResult.Fail(ActionError.GameOver);
        }

        BeginTurn();
        return EndTurn();
    }

    public ActionResult OpenGrille(Position target)
    {
        if (Status != GameStatus.Playing)
        {
            return ActionResult.Fail(ActionError.GameOver);
        }

        var grille = GrilleAt(target);
        if (grille is null)
        {
            return ActionResult.Fail(ActionError.NotAGrille);
        }

        if (!Magus.Position.IsAdjacentTo(target))
        {
            return ActionResult.Fail(ActionError.NotAdjacent);
        }

        if (!Level.IsDoor(target))
        {
            return ActionResult.Fail(ActionError.GrilleNeedsLever);
        }

        if (grille.IsOpen)
        {
            return ActionResult.Fail(ActionError.GrilleAlreadyOpen);
        }

        BeginTurn();
        OpenGate(grille);
        return EndTurn();
    }

    public ActionResult PullLever(Position target)
    {
        if (Status != GameStatus.Playing)
        {
            return ActionResult.Fail(ActionError.GameOver);
        }

        if (Level.LeverTarget(target) is not { } linked || GrilleAt(linked) is not { } grille)
        {
            return ActionResult.Fail(ActionError.NotALever);
        }

        if (!Magus.Position.IsAdjacentTo(target))
        {
            return ActionResult.Fail(ActionError.NotAdjacent);
        }

        if (MonsterAt(target) is not null)
        {
            return ActionResult.Fail(ActionError.OccupiedByMonster);
        }

        if (grille.IsOpen)
        {
            return ActionResult.Fail(ActionError.GrilleAlreadyOpen);
        }

        BeginTurn();
        _events.Add(new LeverPulled(target, linked));
        OpenGate(grille);
        foreach (var other in LeverGrilles(target).Where(position => position != linked))
        {
            if (GrilleAt(other) is { } chained)
            {
                OpenGate(chained);
            }
        }

        return EndTurn();
    }

    public IEnumerable<Position> LeverGrilles(Position lever) =>
        Script is { } script
            ? script.LeverGrilles(Level, lever)
            : Level.LeverTarget(lever) is { } grille ? new[] { grille } : Array.Empty<Position>();

    public ActionResult Activate(Position target)
    {
        if (Status != GameStatus.Playing)
        {
            return ActionResult.Fail(ActionError.GameOver);
        }

        if (Script is not { } script)
        {
            return ActionResult.Fail(ActionError.NotActivable);
        }

        var error = script.CanActivate(this, target);
        if (error != ActionError.None)
        {
            return ActionResult.Fail(error);
        }

        BeginTurn();
        script.Activate(this, target);
        return EndTurn();
    }

    private void OpenGate(GrilleState grille)
    {
        grille.TurnsRemaining = Rules.GrilleOpenDuration;
        grille.OpenedThisTurn = true;
        _events.Add(new GrilleOpened(grille.Position, grille.TurnsRemaining));
    }

    public ActionResult Break(Position target)
    {
        if (Status != GameStatus.Playing)
        {
            return ActionResult.Fail(ActionError.GameOver);
        }

        if (!_containers.TryGetValue(target, out var hasPotion))
        {
            return ActionResult.Fail(ActionError.NotAContainer);
        }

        if (!Magus.Position.IsAdjacentTo(target))
        {
            return ActionResult.Fail(ActionError.NotAdjacent);
        }

        if (MonsterAt(target) is not null)
        {
            return ActionResult.Fail(ActionError.OccupiedByMonster);
        }

        BeginTurn();
        _containers.Remove(target);
        if (hasPotion)
        {
            _potions.Add(target);
        }

        _events.Add(new ContainerBroken(target, hasPotion));
        return EndTurn();
    }

    public ActionResult CastSpacioMagus(Position target)
    {
        if (Status != GameStatus.Playing)
        {
            return ActionResult.Fail(ActionError.GameOver);
        }

        if (Magus.Magic < Rules.SpellCost)
        {
            return ActionResult.Fail(ActionError.NotEnoughMagic);
        }

        var error = CheckTeleport(target);
        if (error != ActionError.None)
        {
            return ActionResult.Fail(error);
        }

        BeginTurn();
        ChangeMagic(Magus.Magic - Rules.SpellCost);
        MoveMagus(target, MoveCause.Teleport);
        foreach (var monster in _monsters)
        {
            if (!HasLineOfSight(monster.Position, Magus.Position))
            {
                LoseTrack(monster);
            }
        }

        return EndTurn();
    }

    public ActionResult CastSpacioCreatura(Position first, Position second)
    {
        if (Status != GameStatus.Playing)
        {
            return ActionResult.Fail(ActionError.GameOver);
        }

        if (Magus.Magic < Rules.SpellCost)
        {
            return ActionResult.Fail(ActionError.NotEnoughMagic);
        }

        if (SwapCandidates().Count < 2)
        {
            return ActionResult.Fail(ActionError.NotEnoughCreatures);
        }

        if (first == second)
        {
            return ActionResult.Fail(ActionError.SameCreature);
        }

        var a = MonsterAt(first);
        var b = MonsterAt(second);
        if (a is null || b is null)
        {
            return ActionResult.Fail(ActionError.NotAMonster);
        }

        var errorA = CheckSwapTarget(a);
        if (errorA != ActionError.None)
        {
            return ActionResult.Fail(errorA);
        }

        var errorB = CheckSwapTarget(b);
        if (errorB != ActionError.None)
        {
            return ActionResult.Fail(errorB);
        }

        if (!CanStand(a.Kind, second) || !CanStand(b.Kind, first))
        {
            return ActionResult.Fail(ActionError.SwapBlocked);
        }

        BeginTurn();
        ChangeMagic(Magus.Magic - Rules.SpellCost);
        a.Position = second;
        b.Position = first;
        _events.Add(new MonsterMoved(a.Id, first, second, MoveCause.Swap));
        _events.Add(new MonsterMoved(b.Id, second, first, MoveCause.Swap));
        _events.Add(new CreaturesSwapped(a.Id, b.Id));
        foreach (var monster in new[] { a, b })
        {
            monster.StunTurns = Rules.StunDuration;
            monster.FearTurns = 0;
            _events.Add(new MonsterStunned(monster.Id, monster.StunTurns));
            LoseTrack(monster);
        }

        return EndTurn();
    }

    private ActionError CheckTeleport(Position target)
    {
        if (target == Magus.Position)
        {
            return ActionError.NotFree;
        }

        if (!Level.InBounds(target) || Magus.Position.ChebyshevTo(target) > Rules.SpellRange)
        {
            return ActionError.OutOfRange;
        }

        if (!IsWalkable(target))
        {
            return ActionError.Blocked;
        }

        if (MonsterAt(target) is not null)
        {
            return ActionError.NotFree;
        }

        return HasLineOfSight(Magus.Position, target) ? ActionError.None : ActionError.NotVisible;
    }

    private ActionError CheckSwapTarget(MonsterState monster)
    {
        if (Magus.Position.ChebyshevTo(monster.Position) > Rules.SpellRange)
        {
            return ActionError.OutOfRange;
        }

        return HasLineOfSight(Magus.Position, monster.Position) ? ActionError.None : ActionError.NotVisible;
    }

    private bool ClearLine(Position from, Position to)
    {
        var nx = Math.Abs(to.X - from.X);
        var ny = Math.Abs(to.Y - from.Y);
        var sx = Math.Sign(to.X - from.X);
        var sy = Math.Sign(to.Y - from.Y);
        var x = from.X;
        var y = from.Y;
        var ix = 0;
        var iy = 0;
        while (ix < nx || iy < ny)
        {
            var decision = (1 + 2 * ix) * ny - (1 + 2 * iy) * nx;
            if (decision == 0)
            {
                if (BlocksSight(new Position(x + sx, y)) && BlocksSight(new Position(x, y + sy)))
                {
                    return false;
                }

                x += sx;
                y += sy;
                ix++;
                iy++;
            }
            else if (decision < 0)
            {
                x += sx;
                ix++;
            }
            else
            {
                y += sy;
                iy++;
            }

            var cell = new Position(x, y);
            if (cell != to && BlocksSight(cell))
            {
                return false;
            }
        }

        return true;
    }

    private void BeginTurn()
    {
        _events = new List<GameEvent>();
        Turn++;
    }

    private ActionResult EndTurn()
    {
        if (CheckVictory())
        {
            return ActionResult.Ok(_events);
        }

        RunMonsters();
        if (Status == GameStatus.Playing)
        {
            RunObjects();
            CheckVictory();
        }

        if (Status == GameStatus.Playing)
        {
            IsStranded = CheckStranded();
        }

        return ActionResult.Ok(_events);
    }

    private ActionResult AnnounceStranded()
    {
        _events = new List<GameEvent> { new MagusStranded(Magus.Position) };
        Status = GameStatus.Lost;
        LossReason = LossReason.Stranded;
        return ActionResult.Ok(_events);
    }

    private bool CheckStranded()
    {
        if (Magus.Magic >= Rules.SpellCost)
        {
            return false;
        }

        var zone = ReachableZone();
        if (zone.Any(position => Level.TerrainAt(position) == Terrain.Stairs || _potions.Contains(position)))
        {
            return false;
        }

        return !_containers.Keys.Any(container => zone.Any(position => position.IsAdjacentTo(container)));
    }

    private HashSet<Position> ReachableZone()
    {
        var openable = new HashSet<Position>(_grilles.Values.Where(grille => grille.IsOpen || Level.IsDoor(grille.Position)).Select(grille => grille.Position));
        while (true)
        {
            var zone = FloodFrom(Magus.Position, openable);
            var grown = false;
            foreach (var lever in Level.Levers.Keys)
            {
                var grilles = LeverGrilles(lever).Where(grille => !openable.Contains(grille)).ToList();
                if (grilles.Count > 0 && zone.Any(position => position.IsAdjacentTo(lever)))
                {
                    openable.UnionWith(grilles);
                    grown = true;
                }
            }

            if (!grown)
            {
                return zone;
            }
        }
    }

    private HashSet<Position> FloodFrom(Position start, HashSet<Position> openable)
    {
        bool Blocks(Position position) => BlocksSight(position) && !openable.Contains(position);
        bool Passable(Position position) =>
            Level.InBounds(position) && !Blocks(position) && !IsObstacleAt(position);

        var zone = new HashSet<Position> { start };
        var pending = new Queue<Position>();
        pending.Enqueue(start);
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            foreach (var direction in Position.Directions)
            {
                var next = current + direction;
                if (zone.Contains(next) || !Passable(next))
                {
                    continue;
                }

                if (direction.X != 0 && direction.Y != 0
                    && (Blocks(new Position(next.X, current.Y)) || Blocks(new Position(current.X, next.Y))))
                {
                    continue;
                }

                zone.Add(next);
                pending.Enqueue(next);
            }
        }

        return zone;
    }

    private bool CheckVictory()
    {
        if (Level.TerrainAt(Magus.Position) != Terrain.Stairs)
        {
            return false;
        }

        Status = GameStatus.Won;
        _events.Add(new LevelCompleted());
        return true;
    }

    private void ChangeMagic(int value)
    {
        var clamped = Math.Clamp(value, 0, Rules.MagicMax);
        if (clamped == Magus.Magic)
        {
            return;
        }

        _events.Add(new MagicChanged(Magus.Magic, clamped));
        Magus.Magic = clamped;
    }

    private void MoveMagus(Position to, MoveCause cause)
    {
        var from = Magus.Position;
        Magus.Position = to;
        _events.Add(new MagusMoved(from, to, cause));
        if (_potions.Contains(to) && Magus.Magic < Rules.MagicMax)
        {
            _potions.Remove(to);
            _events.Add(new PotionPicked(to));
            ChangeMagic(Rules.MagicMax);
        }
    }

    private void LoseTrack(MonsterState monster)
    {
        if (!monster.IsTracking || monster.Kind == MonsterKind.ChauveSouris)
        {
            return;
        }

        monster.IsTracking = false;
        _events.Add(new MonsterLostTrack(monster.Id));
    }

    private void RunMonsters()
    {
        var order = _monsters
            .OrderBy(monster => monster.Position.ChebyshevTo(Magus.Position))
            .ThenBy(monster => monster.Id)
            .ToList();
        foreach (var monster in order)
        {
            ActMonster(monster);
            if (Status != GameStatus.Playing)
            {
                return;
            }
        }
    }

    private void ActMonster(MonsterState monster)
    {
        if (monster.StunTurns > 0)
        {
            monster.StunTurns--;
            if (monster.StunTurns == 0)
            {
                _events.Add(new MonsterRecovered(monster.Id));
            }

            return;
        }

        if (monster.Kind == MonsterKind.ChauveSouris)
        {
            ActBat(monster);
            return;
        }

        if (monster.MutationTurns > 0)
        {
            monster.MutationTurns--;
            if (monster.MutationTurns == 0)
            {
                var from = monster.Kind;
                monster.Kind = Mutated(from);
                _events.Add(new MonsterMutated(monster.Id, from, monster.Kind));
            }

            return;
        }

        var wasAfraid = monster.FearTurns > 0;
        monster.FearTurns = 0;

        if (IsFeared(monster.Kind, monster.Position))
        {
            if (FleeStep(monster) is { } refuge)
            {
                StepMonster(monster, refuge);
                return;
            }

            BlockByFear(monster, wasAfraid);
            return;
        }

        if (!monster.IsTracking && HasLineOfSight(monster.Position, Magus.Position))
        {
            monster.IsTracking = true;
            _events.Add(new MonsterSpotted(monster.Id));
        }

        if (monster.IsTracking)
        {
            var adjacent = monster.Position.IsAdjacentTo(Magus.Position) && IsCornerFree(monster.Position, Magus.Position);
            if (adjacent && !IsFeared(monster.Kind, Magus.Position))
            {
                var from = monster.Position;
                monster.Position = Magus.Position;
                _events.Add(new MonsterMoved(monster.Id, from, monster.Position, MoveCause.Step));
                _events.Add(new MagusCaptured(monster.Id));
                Status = GameStatus.Lost;
                LossReason = LossReason.Captured;
                return;
            }

            if (adjacent)
            {
                BlockByFear(monster, wasAfraid);
                return;
            }

            Chase(monster, wasAfraid);
            return;
        }

        Patrol(monster);
    }

    private void ActBat(MonsterState bat)
    {
        if (!bat.IsTracking && HasLineOfSight(bat.Position, Magus.Position))
        {
            bat.IsTracking = true;
            _events.Add(new MonsterSpotted(bat.Id));
        }

        if (!bat.IsTracking)
        {
            Patrol(bat);
            return;
        }

        if (bat.Position.IsAdjacentTo(Magus.Position) && IsCornerFree(bat.Position, Magus.Position))
        {
            Bite(bat);
            return;
        }

        if (FlightStep(bat.Position, Magus.Position) is { } flight)
        {
            TryStep(bat, flight);
            return;
        }

        if (GatePath(bat.Position, Magus.Position) is { } path)
        {
            if (path.Count > 1)
            {
                TryStep(bat, path[1]);
            }

            return;
        }

        if (ApproachStep(bat) is { } step)
        {
            TryStep(bat, step);
        }
    }

    private List<Position>? GatePath(Position start, Position goal)
    {
        bool IsClosedGate(Position position) => GrilleAt(position) is { IsOpen: false };

        var cameFrom = new Dictionary<Position, Position> { [start] = start };
        var queue = new Queue<Position>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var direction in Position.Directions)
            {
                var next = current + direction;
                if (cameFrom.ContainsKey(next) || !IsPlannableCorner(current, next))
                {
                    continue;
                }

                if (next == goal)
                {
                    var path = new List<Position> { current };
                    while (path[^1] != start)
                    {
                        path.Add(cameFrom[path[^1]]);
                    }

                    path.Reverse();
                    var gate = path.FindIndex(IsClosedGate);
                    return gate < 0 ? null : path.GetRange(0, gate);
                }

                if (!IsClosedGate(next) && (!CanFlyOver(next) || IsOccupied(next)))
                {
                    continue;
                }

                cameFrom[next] = current;
                queue.Enqueue(next);
            }
        }

        return null;
    }

    private Position? ApproachStep(MonsterState bat)
    {
        var start = bat.Position;
        var cameFrom = new Dictionary<Position, Position> { [start] = start };
        var queue = new Queue<Position>();
        queue.Enqueue(start);
        var best = start;
        var bestScore = ApproachScore(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var score = ApproachScore(current);
            if (score.CompareTo(bestScore) < 0)
            {
                best = current;
                bestScore = score;
            }

            foreach (var direction in Position.Directions)
            {
                var next = current + direction;
                if (cameFrom.ContainsKey(next) || !CanFlyOver(next) || IsOccupied(next) || !IsPlannableCorner(current, next))
                {
                    continue;
                }

                cameFrom[next] = current;
                queue.Enqueue(next);
            }
        }

        return best == start ? null : FirstOnPath(cameFrom, start, best);
    }

    private (int Chebyshev, int Squared) ApproachScore(Position position)
    {
        var dx = position.X - Magus.Position.X;
        var dy = position.Y - Magus.Position.Y;
        return (Math.Max(Math.Abs(dx), Math.Abs(dy)), dx * dx + dy * dy);
    }

    private void Bite(MonsterState bat)
    {
        if (Magus.Magic <= 0)
        {
            _events.Add(new MagusFainted(bat.Id));
            Status = GameStatus.Lost;
            LossReason = LossReason.Fainted;
            return;
        }

        ChangeMagic(Magus.Magic - 1);
        _events.Add(new MagusBitten(bat.Id));
        bat.StunTurns = BatSatedTurns;
        _events.Add(new MonsterStunned(bat.Id, bat.StunTurns));
    }

    private bool CanFlyOver(Position position) =>
        Level.InBounds(position)
        && (LevelDefinition.IsFlyable(Level.TerrainAt(position)) || GrilleAt(position) is { IsOpen: true });

    private bool CanStand(MonsterKind kind, Position position) =>
        kind == MonsterKind.ChauveSouris ? CanFlyOver(position) : IsWalkable(position);

    private bool IsPlannableFor(MonsterKind kind, Position position) =>
        kind == MonsterKind.ChauveSouris ? CanFlyOver(position) : IsPlannable(position);

    private Position? FlightStep(Position start, Position goal)
    {
        var cameFrom = new Dictionary<Position, Position> { [start] = start };
        var queue = new Queue<Position>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var direction in Position.Directions)
            {
                var next = current + direction;
                if (cameFrom.ContainsKey(next) || !IsPlannableCorner(current, next))
                {
                    continue;
                }

                if (next == goal)
                {
                    return current == start ? null : FirstOnPath(cameFrom, start, current);
                }

                if (!CanFlyOver(next) || IsOccupied(next))
                {
                    continue;
                }

                cameFrom[next] = current;
                queue.Enqueue(next);
            }
        }

        return null;
    }

    private void Patrol(MonsterState monster)
    {
        var route = monster.Route;
        if (monster.Position == Scheduled(monster, Turn - 1))
        {
            var next = Scheduled(monster, Turn);
            if (next != monster.Position && !IsFeared(monster.Kind, next))
            {
                TryStep(monster, next);
            }

            return;
        }

        var start = monster.Position;
        var cameFrom = new Dictionary<Position, Position> { [start] = start };
        var distances = new Dictionary<Position, int> { [start] = 0 };
        var queue = new Queue<Position>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var direction in Position.Directions)
            {
                var next = current + direction;
                if (distances.ContainsKey(next) || !IsPlannableFor(monster.Kind, next) || !IsPlannableCorner(current, next) || IsFeared(monster.Kind, next))
                {
                    continue;
                }

                distances[next] = distances[current] + 1;
                cameFrom[next] = current;
                queue.Enqueue(next);
            }
        }

        Position? target = null;
        var bestTime = int.MaxValue;
        var bestDistance = int.MaxValue;
        for (var index = 0; index < route.Count; index++)
        {
            if (!distances.TryGetValue(route[index], out var distance))
            {
                continue;
            }

            var arrival = Turn - 1 + distance;
            var time = arrival + Modulo(index - monster.RouteOffset - arrival, route.Count);
            if (time < bestTime || (time == bestTime && distance < bestDistance))
            {
                bestTime = time;
                bestDistance = distance;
                target = route[index];
            }
        }

        if (target is { } goal && goal != start)
        {
            TryStep(monster, FirstOnPath(cameFrom, start, goal));
        }
    }

    private static Position Scheduled(MonsterState monster, int turn) =>
        monster.Route[Modulo(turn + monster.RouteOffset, monster.Route.Count)];

    private static int Modulo(int value, int divisor) => ((value % divisor) + divisor) % divisor;

    private void Chase(MonsterState monster, bool wasAfraid)
    {
        if (FirstStep(monster.Position, Magus.Position, monster.Kind) is { } step)
        {
            TryStep(monster, step);
            return;
        }

        if (FirstStep(monster.Position, Magus.Position, null) is null)
        {
            return;
        }

        if (ClosestApproach(monster) is { } approach)
        {
            TryStep(monster, approach);
            return;
        }

        BlockByFear(monster, wasAfraid);
    }

    private void BlockByFear(MonsterState monster, bool wasAfraid)
    {
        if (!wasAfraid)
        {
            monster.FearTurns = 1;
            _events.Add(new MonsterAfraid(monster.Id));
            return;
        }

        monster.MutationTurns = Rules.MutationDuration - 1;
        _events.Add(new MonsterMutationStarted(monster.Id, Rules.MutationDuration));
        if (monster.MutationTurns == 0)
        {
            var from = monster.Kind;
            monster.Kind = Mutated(from);
            _events.Add(new MonsterMutated(monster.Id, from, monster.Kind));
        }
    }

    private static MonsterKind Mutated(MonsterKind kind) => kind switch
    {
        MonsterKind.BlobVert => MonsterKind.BlobRouge,
        MonsterKind.BlobRouge => MonsterKind.BlobVert,
        _ => kind,
    };

    private void TryStep(MonsterState monster, Position step)
    {
        if (!CanStand(monster.Kind, step) || IsOccupied(step) || !IsCornerFree(monster.Position, step))
        {
            return;
        }

        StepMonster(monster, step);
    }

    private void StepMonster(MonsterState monster, Position step)
    {
        var from = monster.Position;
        monster.Position = step;
        _events.Add(new MonsterMoved(monster.Id, from, step, MoveCause.Step));
    }

    private Position? FleeStep(MonsterState monster)
    {
        var start = monster.Position;
        var cameFrom = new Dictionary<Position, Position> { [start] = start };
        var queue = new Queue<Position>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current != start && !IsFeared(monster.Kind, current))
            {
                var step = FirstOnPath(cameFrom, start, current);
                return IsWalkable(step) && !IsOccupied(step) && IsCornerFree(start, step) ? step : null;
            }

            foreach (var direction in Position.Directions)
            {
                var next = current + direction;
                if (cameFrom.ContainsKey(next) || !IsWalkable(next) || IsOccupied(next) || !IsCornerFree(current, next))
                {
                    continue;
                }

                cameFrom[next] = current;
                queue.Enqueue(next);
            }
        }

        return null;
    }

    private Position? ClosestApproach(MonsterState monster)
    {
        var distances = Distances(Magus.Position);
        var start = monster.Position;
        if (!distances.TryGetValue(start, out var best))
        {
            return null;
        }

        Position? target = null;
        var cameFrom = new Dictionary<Position, Position> { [start] = start };
        var queue = new Queue<Position>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current != start && distances.TryGetValue(current, out var distance) && distance < best && !IsOccupied(current))
            {
                best = distance;
                target = current;
            }

            foreach (var direction in Position.Directions)
            {
                var next = current + direction;
                if (cameFrom.ContainsKey(next) || !IsPlannable(next) || !IsPlannableCorner(current, next) || IsFeared(monster.Kind, next))
                {
                    continue;
                }

                cameFrom[next] = current;
                queue.Enqueue(next);
            }
        }

        return target is { } goal ? FirstOnPath(cameFrom, start, goal) : null;
    }

    private Dictionary<Position, int> Distances(Position origin)
    {
        var distances = new Dictionary<Position, int> { [origin] = 0 };
        var queue = new Queue<Position>();
        queue.Enqueue(origin);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var direction in Position.Directions)
            {
                var next = current + direction;
                if (distances.ContainsKey(next) || !IsPlannable(next) || !IsPlannableCorner(current, next))
                {
                    continue;
                }

                distances[next] = distances[current] + 1;
                queue.Enqueue(next);
            }
        }

        return distances;
    }

    private static Position FirstOnPath(Dictionary<Position, Position> cameFrom, Position start, Position goal)
    {
        var step = goal;
        while (cameFrom[step] != start)
        {
            step = cameFrom[step];
        }

        return step;
    }

    private Position? FirstStep(Position start, Position goal, MonsterKind? afraidOf)
    {
        if (start == goal)
        {
            return null;
        }

        var cameFrom = new Dictionary<Position, Position> { [start] = start };
        var queue = new Queue<Position>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == goal)
            {
                break;
            }

            foreach (var direction in Position.Directions)
            {
                var next = current + direction;
                if (cameFrom.ContainsKey(next) || !IsPlannable(next) || !IsPlannableCorner(current, next))
                {
                    continue;
                }

                if (afraidOf is { } kind && next != goal && IsFeared(kind, next))
                {
                    continue;
                }

                cameFrom[next] = current;
                queue.Enqueue(next);
            }
        }

        return cameFrom.ContainsKey(goal) ? FirstOnPath(cameFrom, start, goal) : null;
    }

    private bool IsPlannable(Position position) =>
        Level.InBounds(position)
        && !LevelDefinition.IsSolid(Level.TerrainAt(position))
        && !IsObstacleAt(position);

    private static HashSet<Position> BuildFireZone(LevelDefinition level)
    {
        var zone = new HashSet<Position>();
        foreach (var torch in level.Torches)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                zone.Add(new Position(torch.X + dx, torch.Y + 1));
            }
        }

        foreach (var brazier in level.Cells.Select((terrain, index) => (terrain, index)).Where(cell => cell.terrain == Terrain.Brazier))
        {
            var center = new Position(brazier.index % level.Width, brazier.index / level.Width);
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    zone.Add(new Position(center.X + dx, center.Y + dy));
                }
            }
        }

        zone.RemoveWhere(position => LevelDefinition.IsSolid(level.TerrainAt(position)));
        return zone;
    }

    private static HashSet<Position> BuildWaterZone(LevelDefinition level)
    {
        var zone = new HashSet<Position>();
        foreach (var water in level.Cells.Select((terrain, index) => (terrain, index)).Where(cell => cell.terrain == Terrain.Water))
        {
            var center = new Position(water.index % level.Width, water.index / level.Width);
            foreach (var direction in Position.Directions)
            {
                var neighbor = center + direction;
                if (level.TerrainAt(neighbor) != Terrain.Water)
                {
                    zone.Add(neighbor);
                }
            }
        }

        zone.RemoveWhere(position => LevelDefinition.IsSolid(level.TerrainAt(position)));
        return zone;
    }

    private bool IsPlannableCorner(Position from, Position to)
    {
        var delta = to - from;
        if (delta.X == 0 || delta.Y == 0)
        {
            return true;
        }

        return IsCornerPlannable(new Position(to.X, from.Y)) && IsCornerPlannable(new Position(from.X, to.Y));
    }

    private bool IsCornerPlannable(Position position) =>
        Level.InBounds(position) && !LevelDefinition.IsSolid(Level.TerrainAt(position));

    private void RunObjects()
    {
        var openGrilles = _grilles.Values
            .Where(grille => grille.IsOpen)
            .OrderBy(grille => grille.Position.Y)
            .ThenBy(grille => grille.Position.X)
            .ToList();
        foreach (var grille in openGrilles)
        {
            if (grille.OpenedThisTurn)
            {
                grille.OpenedThisTurn = false;
                continue;
            }

            grille.TurnsRemaining--;
            if (grille.TurnsRemaining > 0)
            {
                _events.Add(new GrilleCountdown(grille.Position, grille.TurnsRemaining));
                continue;
            }

            if (TryClearGrille(grille))
            {
                _events.Add(new GrilleClosed(grille.Position));
            }
            else
            {
                grille.TurnsRemaining = 1;
                _events.Add(new GrilleCloseDelayed(grille.Position));
            }
        }
    }

    private bool TryClearGrille(GrilleState grille)
    {
        if (!IsOccupied(grille.Position))
        {
            return true;
        }

        var sides = _random.Next(2) == 0
            ? new[] { grille.FirstSide, grille.SecondSide }
            : new[] { grille.SecondSide, grille.FirstSide };

        foreach (var side in sides)
        {
            if (IsWalkable(side) && !IsOccupied(side))
            {
                Push(grille.Position, side);
                return true;
            }
        }

        foreach (var side in sides)
        {
            if (IsWalkable(side) && TryPushChain(side, side - grille.Position))
            {
                Push(grille.Position, side);
                return true;
            }
        }

        return false;
    }

    private bool TryPushChain(Position cell, Position direction)
    {
        var target = cell + direction;
        if (!IsWalkable(target))
        {
            return false;
        }

        if (IsOccupied(target) && !TryPushChain(target, direction))
        {
            return false;
        }

        Push(cell, target);
        return true;
    }

    private void Push(Position from, Position to)
    {
        if (Magus.Position == from)
        {
            MoveMagus(to, MoveCause.Pushed);
            return;
        }

        var monster = MonsterAt(from);
        if (monster is null)
        {
            return;
        }

        monster.Position = to;
        _events.Add(new MonsterMoved(monster.Id, from, to, MoveCause.Pushed));
    }
}
