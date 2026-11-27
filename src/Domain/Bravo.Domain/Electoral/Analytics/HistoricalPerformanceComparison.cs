namespace Bravo.Domain.Electoral.Analytics;

/// <summary>
/// Two elections of the same office compared for one candidate or identity.
/// </summary>
/// <remarks>
/// The comparison is only produced when both elections contest the same office; otherwise the
/// shares are not comparable and the service refuses the request instead of returning a number
/// that looks meaningful.
///
/// <see cref="MunicipalitiesGained"/> and <see cref="MunicipalitiesLost"/> list municipalities
/// where the candidate's own rank moved to or from first place. They do not imply that votes moved
/// between candidates.
/// </remarks>
public sealed record HistoricalPerformanceComparison(
    Guid? CandidateId,
    Guid? CandidateIdentityId,
    string CandidateName,
    string OfficeName,
    HistoricalElectionPerformance Baseline,
    HistoricalElectionPerformance Compared,
    long VoteVariation,
    decimal? VoteVariationPercentage,
    decimal ShareVariationPercentagePoints,
    IReadOnlyList<Guid> MunicipalitiesGained,
    IReadOnlyList<Guid> MunicipalitiesLost,
    IReadOnlyList<Guid> MunicipalitiesImproved,
    IReadOnlyList<Guid> MunicipalitiesDeclined);
