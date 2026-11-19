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
    ImportProgressThroughput Throughput
);
