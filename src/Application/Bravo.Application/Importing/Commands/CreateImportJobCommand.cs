using Bravo.Application.Importing.ViewModels;
using Bravo.Domain.Importing.Enums;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Importing.Commands;

public sealed record CreateImportJobCommand(
    string ImportType,
    IReadOnlyList<string> UnitKeys,
    int? BatchSize = null,
    int? MaxAttempts = null,
    int? LeaseDurationSeconds = null,
    ImportConflictStrategy ConflictStrategy = ImportConflictStrategy.DoNothing)
    : ICommand<Result<ImportJobCreatedViewModel>>;
