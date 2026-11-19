namespace Bravo.Application.Importing.ViewModels;

public sealed record ImportJobCreatedViewModel(
    Guid Id,
    string Status,
    IReadOnlyList<string> WorkUnitKeys
);
