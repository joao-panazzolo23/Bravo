namespace Bravo.Application.Acquisition.Providers;

public record IbgeDatasetInfo(
    string StatesUrl,
    string MunicipalitiesUrl,
    string? Version,
    string Format
);

