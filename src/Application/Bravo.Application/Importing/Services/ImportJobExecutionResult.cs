using Bravo.Domain.Importing.Models;

namespace Bravo.Application.Importing.Services;

/// <summary>
/// The outcome of one execution attempt, as far as the scheduler is concerned.
/// </summary>
/// <remarks>
/// The executor translates the full lifecycle into this small result so the scheduler never has
/// to know about claims, leases or retry classification.
/// </remarks>
public enum ImportJobExecutionOutcome
{
    /// <summary>The job ran and reached a terminal status.</summary>
    Completed,

    /// <summary>Another worker won the claim, or the job was no longer recoverable.</summary>
    NotClaimed,

    /// <summary>The process was shutting down and the job was released for another attempt.</summary>
    ReleasedForShutdown,

    /// <summary>The job was cancelled or lost its lease; the row is already in its final state.</summary>
    Stopped
}

/// <summary>
/// Outcome plus the counters the scheduler logs on completion.
/// </summary>
public sealed record ImportJobExecutionResult(
    ImportJobExecutionOutcome Outcome,
    long ProcessedRecords = 0,
    long FailedRecords = 0);
