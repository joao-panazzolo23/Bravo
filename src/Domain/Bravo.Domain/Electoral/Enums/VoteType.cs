namespace Bravo.Domain.Electoral.Enums;

/// <summary>
/// Classification of a source voting record. Values are persisted as smallint and must not
/// be renumbered once data exists.
/// </summary>
public enum VoteType
{
    Nominal = 1,
    Party = 2,
    Blank = 3,
    Null = 4,
    Other = 5
}
