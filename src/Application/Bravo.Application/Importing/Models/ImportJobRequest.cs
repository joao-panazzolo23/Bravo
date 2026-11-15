namespace Bravo.Application.Importing.Models;

public sealed record ImportJobRequest(
    int? BatchSize = null,
    int? MaxAttempts = null,
    int? LeaseDurationSeconds = null);
