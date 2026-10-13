namespace SpacioMagus.Core.Progress;

public sealed record StarThresholds(int ThreeStars, int TwoStars)
{
    public const int MaxStars = 3;

    public int Rate(int turns) => turns <= ThreeStars ? 3 : turns <= TwoStars ? 2 : 1;
}
