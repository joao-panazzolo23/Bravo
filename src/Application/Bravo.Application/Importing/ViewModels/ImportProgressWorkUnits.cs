namespace Bravo.Application.Importing.ViewModels;

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
