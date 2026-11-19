namespace Bravo.Application.Acquisition.Providers;

public record IbgeMunicipalityRecord(
    int Code,
    string Name,
    int StateCode
);
