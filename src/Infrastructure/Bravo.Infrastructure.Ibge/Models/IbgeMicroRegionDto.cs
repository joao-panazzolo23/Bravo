using System.Text.Json.Serialization;

namespace Bravo.Infrastructure.Ibge.Models;

public record IbgeMicroRegionDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("nome")] string Nome,
    [property: JsonPropertyName("mesorregiao")] IbgeMesoRegionDto? MesoRegion
);
