namespace Bravo.Application.Importing.ViewModels;

public sealed record ImportJobProgressViewModel(
    Guid Id,
    string Status,
    double? WorkUnitPercentage,
    string? CurrentStage,
    string? CurrentWorkUnit,
    double ElapsedSeconds,
    double? EstimatedRemainingSeconds,
    DateTime? LastProgressAt,
    ImportProgressWorkUnits WorkUnits,
    ImportProgressRecords Records,
    ImportProgressThroughput Throughput);

/// <summary>
/// Work-unit completion. <see cref="Percentage"/> is exact because the total is known.
/// </summary>
public sealed record ImportProgressWorkUnits(
    int Total,
    int Completed,
    int Failed,
    int Pending,
    int Running,
    int Retried,
    double Percentage);

/// <summary>
/// Record counters. <see cref="Total"/> and <see cref="Percentage"/> are null when the source
/// does not declare a record count, so no completion percentage is fabricated.
/// </summary>
public sealed record ImportProgressRecords(
    long? Total,
    long Processed,
    long Inserted,
    long Updated,
    long Rejected,
    long Failed,
    double? Percentage);

public sealed record ImportProgressThroughput(
    double? RecordsPerSecond,
    double? WorkUnitsPerMinute);
