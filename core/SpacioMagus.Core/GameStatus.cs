namespace SpacioMagus.Core;

public enum GameStatus
{
    Playing,
    Won,
    Lost,
}

public enum LossReason
{
    None,
    Captured,
    Stranded,
}
