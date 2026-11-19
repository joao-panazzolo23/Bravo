using Bravo.Domain.Importing.Models;
using Bravo.Application.Importing.ViewModels;
using Bravo.Shared.Result;
using Mediator;
using Bravo.Application.Importing.Models;

namespace Bravo.Application.Importing.Commands;

public sealed record StartIbgeImportCommand(
    int? BatchSize = null,
    int? MaxAttempts = null,
    int? LeaseDurationSeconds = null)
    : ICommand<Result<ImportJobCreatedViewModel>>
{
    public ImportJobRequest ToRequest() => new(BatchSize, MaxAttempts, LeaseDurationSeconds);
}
