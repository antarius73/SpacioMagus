namespace SpacioMagus.Core;

public abstract record GameEvent;

public sealed record MagusMoved(Position From, Position To, MoveCause Cause) : GameEvent;

public sealed record MagicChanged(int From, int To) : GameEvent;

public sealed record PotionPicked(Position Position) : GameEvent;

public sealed record MonsterMoved(int MonsterId, Position From, Position To, MoveCause Cause) : GameEvent;

public sealed record MonsterSpotted(int MonsterId) : GameEvent;

public sealed record MonsterLostTrack(int MonsterId) : GameEvent;

public sealed record CreaturesSwapped(int FirstMonsterId, int SecondMonsterId) : GameEvent;

public sealed record MonsterStunned(int MonsterId, int Turns) : GameEvent;

public sealed record MonsterRecovered(int MonsterId) : GameEvent;

public sealed record GrilleOpened(Position Position, int TurnsRemaining) : GameEvent;

public sealed record GrilleCountdown(Position Position, int TurnsRemaining) : GameEvent;

public sealed record GrilleClosed(Position Position) : GameEvent;

public sealed record GrilleCloseDelayed(Position Position) : GameEvent;

public sealed record MonsterAfraid(int MonsterId) : GameEvent;

public sealed record MonsterMutationStarted(int MonsterId, int Turns) : GameEvent;

public sealed record MonsterMutated(int MonsterId, MonsterKind From, MonsterKind To) : GameEvent;

public sealed record MagusCaptured(int MonsterId) : GameEvent;

public sealed record LevelCompleted : GameEvent;
