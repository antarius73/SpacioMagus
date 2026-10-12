namespace SpacioMagus.Core;

public abstract record GameAction;

public sealed record MoveAction(Position Target) : GameAction;

public sealed record WaitAction : GameAction;

public sealed record OpenGrilleAction(Position Target) : GameAction;

public sealed record SpacioMagusAction(Position Target) : GameAction;

public sealed record SpacioCreaturaAction(Position First, Position Second) : GameAction;
