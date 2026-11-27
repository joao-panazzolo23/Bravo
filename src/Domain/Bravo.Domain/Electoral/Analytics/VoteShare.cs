namespace Bravo.Domain.Electoral.Analytics;

/// <summary>
/// A vote count and the share it represents of a denominator.
/// </summary>
/// <remarks>
/// <see cref="Percentage"/> is a share of valid votes (0-100). <see cref="PercentagePoints"/> is
/// the arithmetic difference between two shares and never a relative variation: comparing 50%
/// with 40% is 10 percentage points, not 25%. Both values are rounded to two decimals by the
/// database so clients do not have to replicate the rounding rule.
/// </remarks>
public sealed record VoteShare(long Votes, decimal Percentage)
{
    public static VoteShare Empty { get; } = new(0, 0m);
}
