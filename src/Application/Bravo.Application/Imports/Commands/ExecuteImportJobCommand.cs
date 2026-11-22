using Bravo.Application.Imports.Common.Execution;
using Bravo.Application.Imports.Common.Policies;
using Bravo.Application.Imports.Common.Progress;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Imports.Common.Commands;

/// <summary>
/// Executes one already-scheduled import job.
/// </summary>
/// <remarks>
/// The scheduler dispatches this command through the mediator instead of depending on the
/// executor, the repository or the import sources directly. The command carries the worker
/// identity because lease ownership is part of the execution, not of scheduling.
/// </remarks>
public sealed record ExecuteImportJobCommand(Guid JobId, string WorkerId)
    : ICommand<Result<ImportJobExecutionResult>>;
