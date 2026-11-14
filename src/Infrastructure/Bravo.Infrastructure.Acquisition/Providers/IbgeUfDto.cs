using System.Text.Json.Serialization;

namespace Bravo.Infrastructure.Acquisition.Providers;

public record IbgeFederativeUnitDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("sigla")] string? Sail,
    [property: JsonPropertyName("nome")] string? Name
);
