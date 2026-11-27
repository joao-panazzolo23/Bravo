namespace Bravo.Domain.Electoral.Analytics;

/// <summary>
/// Every candidacy of one candidate identity, aggregated per election and round.
/// </summary>
/// <remarks>
/// <see cref="Candidacies"/> keeps one entry per election round, because an identity can contest
/// several offices and both rounds. <see cref="OfficesContested"/> and
/// <see cref="YearsContested"/> are derived from those rows and never assume that the identity
/// belongs to a party, coalition or ideology: the schema does not model that for an identity.
/// </remarks>
public sealed record CandidateIdentityHistory(
    Guid CandidateIdentityId,
    string FullName,
    IReadOnlyList<HistoricalElectionPerformance> Candidacies,
    IReadOnlyList<string> OfficesContested,
    IReadOnlyList<int> YearsContested,
    long LifetimeVotes);
