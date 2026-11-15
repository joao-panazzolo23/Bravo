using Bravo.Application.Acquisition.Providers;

namespace Bravo.Application.Acquisition.Services;

/// <summary>
/// Bulk persistence of electoral results. Implementations stage rows with PostgreSQL COPY
/// and merge them into the result tables with set-based SQL, so no individual INSERT is
/// issued per imported record.
/// </summary>
public interface IElectoralResultsPersistenceService
{
    /// <summary>
    /// Upserts electoral zones, polling places and polling sections from a parsed batch.
    /// Rows whose state or municipality cannot be resolved are reported as skipped.
    /// </summary>
    Task<PersistenceResult> UpsertPollingSections(
        IReadOnlyList<TsePollingSectionRecord> batch,
        CancellationToken ct = default);

    /// <summary>
    /// Upserts candidate vote facts for a polling section batch.
    /// </summary>
    Task<ResultMerge> UpsertPollingSectionResults(
        Guid electionId,
        int roundNumber,
        Guid provenanceId,
        IReadOnlyList<TsePollingSectionResultRecord> batch,
        CancellationToken ct = default);

    /// <summary>
    /// Upserts turnout statistics for a polling section batch.
    /// </summary>
    Task<ResultMerge> UpsertPollingSectionStatistics(
        Guid electionId,
        int roundNumber,
        Guid provenanceId,
        IReadOnlyList<TsePollingSectionStatisticsRecord> batch,
        CancellationToken ct = default);
}

/// <summary>
/// Outcome of a staged merge: how many rows were inserted, updated and could not be resolved.
/// </summary>
public record ResultMerge(int Inserted, int Updated, int Unresolved, IReadOnlyList<string> UnresolvedSamples)
{
    public static ResultMerge Empty { get; } = new(0, 0, 0, []);
}
