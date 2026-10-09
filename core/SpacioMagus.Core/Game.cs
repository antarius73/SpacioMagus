namespace SpacioMagus.Core;

public sealed class Game
{
    private readonly List<MonsterState> _monsters;
    private readonly Dictionary<Position, GrilleState> _grilles;
    private readonly HashSet<Position> _potions;
    private readonly IRandomSource _random;
    private List<GameEvent> _events = new();

    public Game(LevelDefinition level, RuleSet? rules = null, IRandomSource? random = null)
    {
        Level = level ?? throw new ArgumentNullException(nameof(level));
        Rules = rules ?? RuleSet.Default;
        Rules.Validate();
        _random = random ?? new SystemRandomSource();
        Magus = new MagusState(level.MagusStart, Rules.MagicMax);
        _monsters = level.Monsters
            .Select((spawn, index) => new MonsterState(index + 1, spawn.Kind, spawn.Start, spawn.Post))
            .ToList();
        _grilles = new Dictionary<Position, GrilleState>();
        foreach (var position in level.Grilles())
        {
            var (first, second) = level.GrilleSides(position);
            _grilles[position] = new GrilleState(position, first, second);
        }

        _potions = new HashSet<Position>(level.Potions);
    }

    public LevelDefinition Level { get; }

    public RuleSet Rules { get; }

    public GameStatus Status { get; private set; } = GameStatus.Playing;

    public int Turn { get; private set; }

    public MagusState Magus { get; }

    public IReadOnlyList<MonsterState> Monsters => _monsters;

    public IReadOnlyCollection<GrilleState> Grilles => _grilles.Values;

    public IReadOnlyCollection<Position> Potions => _potions;

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

    public bool IsWalkable(Position position) => !BlocksSight(position);

    public bool IsCornerFree(Position from, Position to)
    {
        var delta = to - from;
        if (delta.X == 0 || delta.Y == 0)
        {
            return true;
        }

        return IsWalkable(new Position(to.X, from.Y)) && IsWalkable(new Position(from.X, to.Y));
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
        ChangeMagic(Magus.Magic + Rules.RegenPerTurn);
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

        if (grille.IsOpen)
        {
            return ActionResult.Fail(ActionError.GrilleAlreadyOpen);
        }

        BeginTurn();
        grille.TurnsRemaining = Rules.GrilleOpenDuration;
        grille.OpenedThisTurn = true;
        _events.Add(new GrilleOpened(target, grille.TurnsRemaining));
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

        return ActionResult.Ok(_events);
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
        if (!monster.IsTracking)
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

        if (!monster.IsTracking && HasLineOfSight(monster.Position, Magus.Position))
        {
            monster.IsTracking = true;
            _events.Add(new MonsterSpotted(monster.Id));
        }

        if (monster.IsTracking)
        {
            if (monster.Position.IsAdjacentTo(Magus.Position) && IsCornerFree(monster.Position, Magus.Position))
            {
                var from = monster.Position;
                monster.Position = Magus.Position;
                _events.Add(new MonsterMoved(monster.Id, from, monster.Position, MoveCause.Step));
                _events.Add(new MagusCaptured(monster.Id));
                Status = GameStatus.Lost;
                return;
            }

            StepTowards(monster, Magus.Position);
            return;
        }

        if (monster.Position != monster.Post)
        {
            StepTowards(monster, monster.Post);
        }
    }

    private void StepTowards(MonsterState monster, Position goal)
    {
        if (FirstStep(monster.Position, goal) is not { } step)
        {
            return;
        }

        if (!IsWalkable(step) || IsOccupied(step) || !IsCornerFree(monster.Position, step))
        {
            return;
        }

        var from = monster.Position;
        monster.Position = step;
        _events.Add(new MonsterMoved(monster.Id, from, step, MoveCause.Step));
    }

    private Position? FirstStep(Position start, Position goal)
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

                cameFrom[next] = current;
                queue.Enqueue(next);
            }
        }

        if (!cameFrom.ContainsKey(goal))
        {
            return null;
        }

        var step = goal;
        while (cameFrom[step] != start)
        {
            step = cameFrom[step];
        }

        return step;
    }

    private bool IsPlannable(Position position) =>
        Level.InBounds(position) && !LevelDefinition.IsSolid(Level.TerrainAt(position));

    private bool IsPlannableCorner(Position from, Position to)
    {
        var delta = to - from;
        if (delta.X == 0 || delta.Y == 0)
        {
            return true;
        }

        return IsPlannable(new Position(to.X, from.Y)) && IsPlannable(new Position(from.X, to.Y));
    }

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
