using System.Text.Json.Serialization;

namespace Bravo.Application.Imports.Common.ViewModels;

/// <summary>
/// Response returned when an import job is accepted.
/// </summary>
/// <remarks>
/// <see cref="OperationKeys"/> keeps its historical JSON name so the creation response stays
/// compatible with clients built against the work-unit vocabulary.
/// </remarks>
public sealed record ImportJobCreatedViewModel(
    Guid Id,
    string Status,
    [property: JsonPropertyName("workUnitKeys")] IReadOnlyList<string> OperationKeys
);
