namespace Bravo.Domain.Electoral.Analytics;

/// <summary>
/// One polling station in one election round with the vote composition of a single candidate.
/// </summary>
/// <remarks>
/// <c>SectionId</c> is the stored result granularity. A polling place can host several sections,
/// so a row here is one section, and the polling place is carried alongside for context instead of
/// being summed implicitly with its neighbours.
///
/// <see cref="DifferenceVotes"/> and <see cref="DifferencePercentagePoints"/> compare the
/// candidate against the aggregate of every other candidate, never against a specific runner-up.
/// </remarks>
public sealed record PollingStationVoteConcentration(
    int Year,
    Guid ElectionId,
    string OfficeName,
    short Round,
    bool IsRunoff,
    Guid SectionId,
    int TseSectionNumber,
    Guid? PollingPlaceId,
    int? TsePollingPlaceNumber,
    string? PollingPlaceName,
    Guid MunicipalityId,
    string MunicipalityName,
    Guid StateId,
    string StateAbbreviation,
    Guid ZoneId,
    int TseZoneNumber,
    Guid? CandidateId,
    string CandidateName,
    string? CandidateUrnName,
    Guid? CandidateIdentityId,
    string? CandidateIdentityName,
    Guid? PartyId,
    string? PartyAcronym,
    long ValidVotes,
    long CandidateVotes,
    long OpposingVotes,
    decimal CandidatePercentage,
    decimal OpposingPercentage,
    long DifferenceVotes,
    decimal DifferencePercentagePoints);
