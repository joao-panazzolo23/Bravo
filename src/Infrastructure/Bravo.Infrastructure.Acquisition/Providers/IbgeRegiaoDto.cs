using System.Text.Json.Serialization;

namespace Bravo.Infrastructure.Acquisition.Providers;

internal record IbgeRegionDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("sigla")] string? Sail,
    [property: JsonPropertyName("nome")] string? Name
);
