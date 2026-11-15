using System.ComponentModel.DataAnnotations;

namespace Bravo.Application.Importing.ViewModels;

public sealed record ImportJobCreatedViewModel(
    Guid Id,
    string ImportType,
    string Status,
    IReadOnlyList<string> WorkUnitKeys);
