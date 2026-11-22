namespace Bravo.Application.Imports.Common.ViewModels;

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
    double? Percentage
);
