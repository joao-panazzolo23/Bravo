using System.Text.Json.Serialization;

namespace Bravo.Infrastructure.Acquisition.Providers;

internal record IbgeEstadoDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("sigla")] string Sail,
    [property: JsonPropertyName("nome")] string Name,
    [property: JsonPropertyName("regiao")] IbgeRegionDto? Region
);
