namespace Bravo.Domain.Electoral.Analytics;

/// <summary>
/// One election round and the performance of a candidate or identity inside it.
/// </summary>
/// <remarks>
/// <see cref="VotePercentage"/> is the share of valid votes for the filtered scope. The
/// municipality counters only consider municipalities where the candidate had nominal votes, so a
/// municipality where the candidacy was absent is neither a win nor a loss.
/// </remarks>
public sealed record HistoricalElectionPerformance(
    int Year,
    Guid ElectionId,
    string OfficeName,
    short Round,
    Guid? CandidateId,
    string CandidateName,
    Guid? CandidateIdentityId,
    long TotalVotes,
    long ValidVotes,
    decimal VotePercentage,
    int MunicipalitiesWithVotes,
    int MunicipalitiesWon,
    decimal? AverageMunicipalityPercentage,
    Guid? StrongestMunicipalityId,
    string? StrongestMunicipalityName,
    decimal? StrongestMunicipalityPercentage);
