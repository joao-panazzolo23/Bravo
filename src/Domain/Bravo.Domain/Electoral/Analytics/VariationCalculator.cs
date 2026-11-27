namespace Bravo.Domain.Electoral.Analytics;

/// <summary>
/// Percentage variation of a metric between two observations.
/// </summary>
/// <remarks>
/// The result is <see langword="null"/> when the baseline is zero: a growth from nothing has no
/// meaningful ratio, and returning 100% or infinity would be a fabricated number. Callers report the
/// absolute variation in that case.
/// </remarks>
public static class VariationCalculator
{
    public static decimal? Relative(long difference, long baseline)
        => baseline == 0
            ? null
            : Math.Round(100m * difference / baseline, 2);
}
