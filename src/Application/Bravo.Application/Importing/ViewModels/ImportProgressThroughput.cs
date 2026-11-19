namespace Bravo.Application.Importing.ViewModels;

public sealed record ImportProgressThroughput(
    double? RecordsPerSecond,
    double? WorkUnitsPerMinute);
