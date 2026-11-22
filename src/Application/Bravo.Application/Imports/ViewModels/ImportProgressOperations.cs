namespace Bravo.Application.Imports.Common.ViewModels;

/// <summary>
/// Operation (job-level unit of work) completion. <see cref="Percentage"/> is exact because the
/// source declares how many operations it will execute.
/// </summary>
/// <remarks>
/// The type used to be called <c>ImportProgressWorkUnits</c>, from a model that no longer exists:
/// there are no persisted work units, only the job's operation counters. The C# name now matches
/// the domain, while the JSON property that carries it keeps its historical name.
/// </remarks>
public sealed record ImportProgressOperations(
    int Total,
    int Completed,
    int Failed,
    int Pending,
    int Running,
    int Retried,
    double Percentage);
