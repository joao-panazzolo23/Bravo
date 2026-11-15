using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Importing.Commands;

public sealed record CancelImportJobCommand(Guid JobId) : ICommand<Result<Unit>>;
