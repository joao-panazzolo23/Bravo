namespace Bravo.Domain.Electoral.Analytics;

/// <summary>
/// A municipality, its two strongest candidates in one round, and the distance between them.
/// </summary>
/// <remarks>
/// <see cref="MarginVotes"/> is an absolute count and <see cref="MarginPercentagePoints"/> is a
/// difference of valid-vote shares. The winner is decided by nominal votes; the margin is
/// meaningful even when more than two candidates competed because it always uses the top two.
/// </remarks>
public sealed record MunicipalityElectoralMargin(
    int Year,
    Guid ElectionId,
    string OfficeName,
    short Round,
    Guid MunicipalityId,
    string MunicipalityName,
    Guid StateId,
    string StateAbbreviation,
    Guid? WinnerCandidateId,
    string WinnerName,
    Guid? WinnerPartyId,
    string? WinnerPartyAcronym,
    long WinnerVotes,
    decimal WinnerPercentage,
    Guid? RunnerUpCandidateId,
    string? RunnerUpName,
    Guid? RunnerUpPartyId,
    string? RunnerUpPartyAcronym,
    long RunnerUpVotes,
    decimal RunnerUpPercentage,
    long MarginVotes,
    decimal MarginPercentagePoints,
    bool IsTie);
