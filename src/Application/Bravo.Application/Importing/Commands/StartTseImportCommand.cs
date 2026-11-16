using Bravo.Application.Importing.Models;
using Bravo.Application.Importing.ViewModels;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Importing.Commands;

public sealed record StartTseImportCommand(
    int Year,
    string State,
    short Round,
    int ElectionCode,
    int? BatchSize = null,
    int? MaxAttempts = null,
    int? LeaseDurationSeconds = null)
    : ICommand<Result<ImportJobCreatedViewModel>>
{
    public ImportJobRequest ToRequest() => new(BatchSize, MaxAttempts, LeaseDurationSeconds);
}
