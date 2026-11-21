using System.ComponentModel.DataAnnotations;

namespace Bravo.Application.Importing.Options;

/// <summary>
/// Process-wide limits on the expensive, memory-heavy work an import can have in flight.
/// </summary>
/// <remarks>
/// A job's own parallelism is not a safe memory bound: several jobs (or several scopes inside
/// one job) each honouring their own limit multiply the real peak. These limits are therefore
/// global to the process and are enforced through <c>ImportResourceGovernor</c>.
///
/// Downloads, extraction, parsing and persistence have separate budgets on purpose. A big
/// results file should not be able to starve download slots, and a slow database should not
/// block extraction.
/// </remarks>
public sealed class ImportResourceOptions
{
    public const string SectionName = "Importing:Resources";

    /// <summary>
    /// Maximum result scopes (one large CSV at a time) processed concurrently across all jobs.
    /// </summary>
    [Range(1, 64)]
    public int MaxConcurrentResultScopes { get; set; } = 2;

    /// <summary>Maximum candidate scopes processed concurrently across all jobs.</summary>
    [Range(1, 64)]
    public int MaxConcurrentCandidateScopes { get; set; } = 2;

    /// <summary>Maximum simultaneous archive extractions.</summary>
    [Range(1, 32)]
    public int MaxConcurrentExtractions { get; set; } = 1;

    /// <summary>
    /// Maximum simultaneous CSV readers. Parsing is CPU-bound and each reader holds its own
    /// file buffer, so this is kept small independently of scope concurrency.
    /// </summary>
    [Range(1, 64)]
    public int MaxConcurrentCsvReaders { get; set; } = 2;

    /// <summary>Maximum simultaneous result-batch writes to PostgreSQL.</summary>
    [Range(1, 64)]
    public int MaxConcurrentPersistenceOperations { get; set; } = 2;

    /// <summary>
    /// How many batches one scope may have queued for persistence before the parser waits.
    /// </summary>
    [Range(1, 16)]
    public int MaxInFlightBatchesPerScope { get; set; } = 2;
}
