using System.Text.Json.Serialization;

namespace Bravo.Infrastructure.Acquisition.Models;

public record IbgeImmediateRegionDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("nome")] string Name,
    [property: JsonPropertyName("regiao-intermediaria")] IbgeIntermediateRegionDto? IntermediateRegion
);
