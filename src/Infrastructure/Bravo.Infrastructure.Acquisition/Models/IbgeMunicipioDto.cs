using System.Text.Json.Serialization;

namespace Bravo.Infrastructure.Acquisition.Models;

public record IbgeTownDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("nome")] string Nome,
    [property: JsonPropertyName("UF")] IbgeFederativeUnitDto? FederativeUnit
);
