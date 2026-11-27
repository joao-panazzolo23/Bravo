namespace Bravo.Domain.Electoral.Analytics;

/// <summary>
/// Assembles the candidacy history of one identity.
/// </summary>
/// <remarks>
/// The history is a projection over already-aggregated performances, so it never re-reads votes.
/// Offices and years are derived from the contests the identity actually appears in; nothing states
/// or implies a party, coalition or ideological affiliation, because the schema does not attribute
/// those to an identity.
/// </remarks>
public static class IdentityHistoryBuilder
{
    public static CandidateIdentityHistory Build(
        Guid candidateIdentityId,
        string fullName,
        IReadOnlyList<HistoricalElectionPerformance> candidacies)
    {
        ArgumentNullException.ThrowIfNull(fullName);
        ArgumentNullException.ThrowIfNull(candidacies);

        var ordered = candidacies
            .OrderBy(candidacy => candidacy.Year)
            .ThenBy(candidacy => candidacy.Round)
            .ToArray();

        return new CandidateIdentityHistory(
            candidateIdentityId,
            fullName,
            ordered,
            ordered.Select(candidacy => candidacy.OfficeName).Distinct(StringComparer.Ordinal).ToArray(),
            ordered.Select(candidacy => candidacy.Year).Distinct().Order().ToArray(),
            ordered.Sum(candidacy => candidacy.TotalVotes));
    }
}
