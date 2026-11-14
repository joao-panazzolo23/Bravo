using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Acquisition.Commands;

public record CancelImportCommand(Guid ExecutionId)
    : ICommand<Result<Unit>>;

