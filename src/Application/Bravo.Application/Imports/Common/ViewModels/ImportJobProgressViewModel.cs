using System.Text.Json.Serialization;

namespace Bravo.Application.Imports.Common.ViewModels;

/// <summary>
/// Progress of one import job as exposed to clients.
/// </summary>
/// <remarks>
/// The properties describe job-level operations, which is what the importer actually tracks.
/// <see cref="CurrentWorkUnit"/> and <see cref="WorkUnits"/> keep their historical JSON names
/// through <see cref="JsonPropertyNameAttribute"/> so the public contract does not change while the
/// C# model stops referring to a concept that no longer exists.
/// </remarks>
public sealed record ImportJobProgressViewModel(
    Guid Id,
    string Status,
    double? OperationPercentage,
    string? CurrentStage,
    [property: JsonPropertyName("currentWorkUnit")] string? CurrentOperation,
    double ElapsedSeconds,
    double? EstimatedRemainingSeconds,
    DateTime? LastProgressAt,
    [property: JsonPropertyName("workUnits")] ImportProgressOperations Operations,
    ImportProgressRecords Records,
    ImportProgressThroughput Throughput
);
