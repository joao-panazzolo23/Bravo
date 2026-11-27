namespace Bravo.Domain.Electoral.Analytics;

/// <summary>
/// One municipality and how a single candidate performed in it during one election round.
/// </summary>
/// <remarks>
/// <see cref="RankAmongCandidates"/> is the candidate's position by nominal votes inside the
/// municipality for that round and office, so it is meaningful with any number of candidates.
/// </remarks>
public sealed record MunicipalityCandidatePerformance(
    int Year,
    Guid ElectionId,
    string OfficeName,
    short Round,
    Guid MunicipalityId,
    string MunicipalityName,
    Guid StateId,
    string StateAbbreviation,
    Guid? CandidateId,
    string CandidateName,
    Guid? CandidateIdentityId,
    string? CandidateIdentityName,
    long Votes,
    long ValidVotes,
    decimal VotePercentage,
    int RankAmongCandidates);
