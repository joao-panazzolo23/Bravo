using Bravo.Domain.Importing.Enums;

namespace Bravo.Application.Importing.Services;

/// <summary>
/// Immutable view of a work unit's progress at a point in time.
/// </summary>
/// <remarks>
/// <see cref="TotalRecords"/> and <see cref="RecordsPerSecond"/> are nullable because they are
/// genuinely unknown in some sources; they are never defaulted to zero.
/// </remarks>
public sealed record ImportProgressSnapshot(
    ImportStage Stage,
    long ProcessedRecords,
    long InsertedRecords,
    long UpdatedRecords,
    long RejectedRecords,
    long? TotalRecords,
    TimeSpan Elapsed,
    double? RecordsPerSecond
);
