namespace Bravo.Domain.Electoral.Analytics;

/// <summary>
/// A section whose candidate share deviates from its reference population.
/// </summary>
/// <remarks>
/// The reference statistics are computed over all sections of the same municipality, zone or
/// state in the same round and office. <see cref="DeviationRatio"/> divides the deviation by the
/// voter-weighted standard deviation, so the score is a Z-score and collapses to zero when the
/// population has no dispersion. <see cref="IsOutlier"/> only asserts a statistical deviation, not
/// an irregularity: nothing here should be read as evidence of fraud.
/// </remarks>
public sealed record SectionVoteOutlier(
    int Year,
    Guid ElectionId,
    string OfficeName,
    short Round,
    OutlierReferenceLevel ReferenceLevel,
    Guid SectionId,
    int TseSectionNumber,
    Guid MunicipalityId,
    string MunicipalityName,
    Guid StateId,
    string StateAbbreviation,
    Guid ZoneId,
    int TseZoneNumber,
    Guid? CandidateId,
    string? CandidateName,
    long SectionVotes,
    long SectionValidVotes,
    decimal SectionPercentage,
    long ReferenceVotes,
    long ReferenceValidVotes,
    decimal ReferencePercentage,
    decimal ReferenceStandardDeviation,
    decimal DeviationPercentagePoints,
    decimal? DeviationRatio,
    long ReferenceSectionCount,
    bool IsOutlier);
