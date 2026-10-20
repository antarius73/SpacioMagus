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

public sealed record LeverPulled(Position Lever, Position Grille) : GameEvent;

public sealed record LeverReset(Position Lever) : GameEvent;

public sealed record GrilleCountdown(Position Position, int TurnsRemaining) : GameEvent;

public sealed record GrilleClosed(Position Position) : GameEvent;

public sealed record GrilleCloseDelayed(Position Position) : GameEvent;

public sealed record MonsterAfraid(int MonsterId) : GameEvent;

public sealed record MonsterMutationStarted(int MonsterId, int Turns) : GameEvent;

public sealed record MonsterMutated(int MonsterId, MonsterKind From, MonsterKind To) : GameEvent;

public sealed record ContainerBroken(Position Position, bool HadPotion) : GameEvent;

public sealed record MagusCaptured(int MonsterId) : GameEvent;

public sealed record MagusStranded(Position Position) : GameEvent;

public sealed record MagusBitten(int MonsterId) : GameEvent;

public sealed record MagusFainted(int MonsterId) : GameEvent;

public sealed record AlcoveLit(Position Position) : GameEvent;

public sealed record PotionsRevealed : GameEvent;

public sealed record ChestToggled(Position Position, bool Open) : GameEvent;

public sealed record KeyPicked(Position Position) : GameEvent;

public sealed record DoorUnlocked(Position Position) : GameEvent;

public sealed record SpikesRaised(Position Position) : GameEvent;

public sealed record SpikesLowered(Position Position) : GameEvent;

public sealed record MagusImpaled(Position Position) : GameEvent;

public sealed record ObjectMoved(Position From, Position To) : GameEvent;

public sealed record ObjectsSwapped(Position First, Position Second) : GameEvent;

public sealed record ScrollTaken(Position Position) : GameEvent;

public sealed record LevelCompleted : GameEvent;
