using SpacioMagus.Core;

namespace SpacioMagus.Analysis;

public sealed class ForcedRandom : IRandomSource
{
    public ForcedRandom(int value)
    {
        Value = value;
    }

    public int Value { get; }

    public int Next(int maxExclusive) => Math.Clamp(Value, 0, maxExclusive - 1);
}
