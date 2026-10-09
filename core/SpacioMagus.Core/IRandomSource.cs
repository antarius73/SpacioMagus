namespace SpacioMagus.Core;

public interface IRandomSource
{
    int Next(int maxExclusive);
}

public sealed class SystemRandomSource : IRandomSource
{
    private readonly Random _random;

    public SystemRandomSource(int? seed = null)
    {
        _random = seed is { } value ? new Random(value) : new Random();
    }

    public int Next(int maxExclusive) => _random.Next(maxExclusive);
}
