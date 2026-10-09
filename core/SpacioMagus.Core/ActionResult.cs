namespace SpacioMagus.Core;

public sealed class ActionResult
{
    private static readonly IReadOnlyList<GameEvent> NoEvents = Array.Empty<GameEvent>();

    private ActionResult(ActionError error, IReadOnlyList<GameEvent> events)
    {
        Error = error;
        Events = events;
    }

    public ActionError Error { get; }

    public bool Success => Error == ActionError.None;

    public IReadOnlyList<GameEvent> Events { get; }

    internal static ActionResult Ok(IReadOnlyList<GameEvent> events) => new(ActionError.None, events);

    internal static ActionResult Fail(ActionError error) => new(error, NoEvents);
}
