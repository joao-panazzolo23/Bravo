using System.Text.Json.Serialization;

namespace Bravo.Infrastructure.Ibge.Models;

public record IbgeMesoRegionDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("nome")] string Name,
    [property: JsonPropertyName("UF")] IbgeFederativeUnitDto? FederativeUnit
);
