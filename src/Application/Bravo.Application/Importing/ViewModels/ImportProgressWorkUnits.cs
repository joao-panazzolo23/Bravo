namespace Bravo.Application.Importing.ViewModels;

/// <summary>
/// Operation (job-level unit of work) completion. <see cref="Percentage"/> is exact because the
/// source declares how many operations it will execute.
/// </summary>
public sealed record ImportProgressWorkUnits(
    int Total,
    int Completed,
    int Failed,
    int Pending,
    int Running,
    int Retried,
    double Percentage
);
