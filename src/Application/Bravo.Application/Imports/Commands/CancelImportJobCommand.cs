using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Imports.Common.Commands;

public sealed record CancelImportJobCommand(Guid JobId) : ICommand<Result<Unit>>;
