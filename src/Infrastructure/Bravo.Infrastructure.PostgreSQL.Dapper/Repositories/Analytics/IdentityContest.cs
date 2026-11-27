namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Analytics;

/// <summary>
/// One (election, round, office) combination a candidate identity competed in.
/// </summary>
internal sealed record IdentityContest(
    int Year,
    Guid ElectionId,
    Guid OfficeId,
    short Round);
