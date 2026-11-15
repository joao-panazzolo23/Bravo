using System.Text.Json.Serialization;

namespace Bravo.Infrastructure.Acquisition.Models;

public record IbgeMunicipalityDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("nome")] string Nome,
    [property: JsonPropertyName("microrregiao")] IbgeMicroRegionDto? MicroRegion,
    [property: JsonPropertyName("regiao-imediata")] IbgeImmediateRegionDto? ImmediateRegion
);
