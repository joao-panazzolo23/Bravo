namespace Bravo.Application.Importing.Models;

/// <summary>
/// Aggregated work-unit counts for a job.
/// </summary>
public sealed record ImportJobWorkUnitProgress(
    int Total,
    int Completed,
    int Failed,
    int Pending,
    int Running,
    int Cancelled,
    int Retried);
