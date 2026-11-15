using System.Text.Json.Serialization;

namespace Bravo.Infrastructure.Acquisition.Providers;

/// <summary>
/// The municipalities endpoint returns the federative unit nested under
/// mesorregião -> UF (and again under região imediata -> região intermediária -> UF).
/// Older/alternative payloads expose a top-level "UF". Both shapes are accepted.
/// </summary>
internal record IbgeTownDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("nome")] string Nome,
    [property: JsonPropertyName("UF")] IbgeFederativeUnitDto? FederativeUnit,
    [property: JsonPropertyName("microrregiao")] IbgeMicroRegionDto? MicroRegion,
    [property: JsonPropertyName("regiao-imediata")] IbgeImmediateRegionDto? ImmediateRegion
)
{
    public IbgeFederativeUnitDto? ResolveState() =>
        FederativeUnit
        ?? MicroRegion?.MesoRegion?.State
        ?? ImmediateRegion?.IntermediateRegion?.State;
}

internal record IbgeMicroRegionDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("nome")] string? Name,
    [property: JsonPropertyName("mesorregiao")] IbgeMesoRegionDto? MesoRegion
);

internal record IbgeMesoRegionDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("nome")] string? Name,
    [property: JsonPropertyName("UF")] IbgeFederativeUnitDto? State
);

internal record IbgeImmediateRegionDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("nome")] string? Name,
    [property: JsonPropertyName("regiao-intermediaria")] IbgeIntermediateRegionDto? IntermediateRegion
);

internal record IbgeIntermediateRegionDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("nome")] string? Name,
    [property: JsonPropertyName("UF")] IbgeFederativeUnitDto? State
);
