using System.Text.Json.Serialization;

namespace Bravo.Infrastructure.Acquisition.Providers;

public record IbgeUfDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("sigla")] string? Sigla,
    [property: JsonPropertyName("nome")] string? Nome
);

