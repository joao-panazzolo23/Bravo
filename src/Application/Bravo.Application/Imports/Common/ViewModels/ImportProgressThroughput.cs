using System.Text.Json.Serialization;

namespace Bravo.Application.Imports.Common.ViewModels;

/// <summary>
/// Throughput of an import job.
/// </summary>
/// <remarks>
/// <see cref="OperationsPerMinute"/> keeps the historical JSON name so the progress response stays
/// compatible with clients built against the work-unit vocabulary.
/// </remarks>
public sealed record ImportProgressThroughput(
    double? RecordsPerSecond,
    [property: JsonPropertyName("workUnitsPerMinute")] double? OperationsPerMinute);
