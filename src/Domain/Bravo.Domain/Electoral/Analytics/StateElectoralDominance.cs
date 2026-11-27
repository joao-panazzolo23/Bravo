namespace Bravo.Domain.Electoral.Analytics;

/// <summary>
/// Dominance of a candidate across municipalities of a state in one round.
/// </summary>
/// <remarks>
/// <see cref="MunicipalitiesWon"/> counts municipalities where the candidate ranked first by
/// nominal votes. A municipality where the candidacy received no votes is counted in
/// <see cref="MunicipalitiesContested"/> only when it had valid votes, so an absent candidacy is
/// not silently treated as a defeat in every municipality of the state.
/// </remarks>
public sealed record StateElectoralDominance(
    int Year,
    Guid ElectionId,
    string OfficeName,
    short Round,
    Guid StateId,
    string StateAbbreviation,
    Guid? CandidateId,
    string? CandidateName,
    Guid? CandidateIdentityId,
    long TotalVotes,
    long ValidVotes,
    decimal VotePercentage,
    int MunicipalitiesContested,
    int MunicipalitiesWon,
    decimal? MunicipalitiesWonPercentage,
    decimal? AverageMunicipalityPercentage,
    decimal? HighestMunicipalityPercentage,
    decimal? LowestMunicipalityPercentage,
    decimal? AverageWinningMarginPercentagePoints);
