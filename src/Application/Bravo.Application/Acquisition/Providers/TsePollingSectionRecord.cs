namespace Bravo.Application.Acquisition.Providers;

/// <summary>
/// A polling section reference parsed from a TSE dataset. Sections are created before
/// results so that results can resolve their geographic foreign keys.
/// </summary>
public record TsePollingSectionRecord(
    string StateAbbreviation,
    string MunicipalityTseCode,
    string ZoneNumber,
    string SectionNumber,
    string PollingPlaceCode,
    string? PollingPlaceName,
    string? PollingPlaceAddress);
