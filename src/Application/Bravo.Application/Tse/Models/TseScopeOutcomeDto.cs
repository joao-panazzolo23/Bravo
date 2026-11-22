namespace Bravo.Application.Tse.Models;

/// <summary>
/// Counts for one (year, state) scope: how many rows were persisted and how many the source could
/// not resolve.
/// </summary>
public readonly record struct TseScopeOutcomeDto(long Imported, long Rejected)
{
    public static TseScopeOutcomeDto Empty => new(0, 0);
}
