using Bravo.Application.Acquisition.ViewModels;
using Bravo.Domain.Acquisition.Enums;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Acquisition.Commands;

public record StartImportCommand(ImportOperationType OperationType, string? Scope = null)
    : ICommand<Result<StartImportResult>>;

