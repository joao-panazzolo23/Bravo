namespace Bravo.Domain.Electoral.Analytics;

/// <summary>
/// Pairwise comparison of the same candidate or identity in two election rounds of the same
/// contest.
/// </summary>
/// <remarks>
/// <see cref="VoteVariationPercentage"/> is a relative change of the vote count, while
/// <see cref="ShareVariationPercentagePoints"/> is a difference of shares. They answer different
/// questions and are deliberately kept apart.
///
/// The municipality counters compare only municipalities present in both rounds. They measure the
/// candidate's own movement, not transfers: a vote lost by an eliminated candidate is never
/// attributed to a surviving one.
/// </remarks>
public sealed record ElectionRoundComparison(
    int Year,
    Guid ElectionId,
    string OfficeName,
    short BaselineRound,
    short ComparedRound,
    long BaselineVotes,
    long ComparedVotes,
    decimal BaselinePercentage,
    decimal ComparedPercentage,
    long VoteVariation,
    decimal? VoteVariationPercentage,
    decimal ShareVariationPercentagePoints,
    int MunicipalitiesCompared,
    int MunicipalitiesImproved,
    int MunicipalitiesDeclined,
    int MunicipalitiesUnchanged);
