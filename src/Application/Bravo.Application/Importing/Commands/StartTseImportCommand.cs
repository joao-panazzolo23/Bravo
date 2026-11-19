using Bravo.Application.Importing.Models;
using Bravo.Application.Importing.ViewModels;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Importing.Commands;

/// <summary>
/// Triggers the full TSE electoral import: every year TSE publishes, every state, every
/// round. There are intentionally no filters — this is an unbounded historical import.
/// </summary>
public sealed record StartTseImportCommand(
    int? BatchSize = null,
    int? MaxAttempts = null,
    int? LeaseDurationSeconds = null)
    : ICommand<Result<ImportJobCreatedViewModel>>
{
    public ImportJobRequest ToRequest() => new(BatchSize, MaxAttempts, LeaseDurationSeconds);
}
