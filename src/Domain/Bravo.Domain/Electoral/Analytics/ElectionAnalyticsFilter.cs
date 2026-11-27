namespace Bravo.Domain.Electoral.Analytics;

/// <summary>
/// Shared scope for every analytical query.
/// </summary>
/// <remarks>
/// A result is only meaningful inside one office and one round: valid votes of different offices
/// share the same ballot but not the same denominator, and rounds are separate contests. Callers
/// may narrow the scope further with election, candidate, identity and geographic identifiers.
/// Every identifier is optional at the type level and validated by the analysis that needs it.
/// </remarks>
public sealed record ElectionAnalyticsFilter(
    int? Year = null,
    Guid? ElectionId = null,
    Guid? OfficeId = null,
    short? Round = null,
    Guid? CandidateId = null,
    Guid? CandidateIdentityId = null,
    Guid? StateId = null,
    Guid? MunicipalityId = null,
    Guid? ZoneId = null)
{
    /// <summary>Requires the office that defines the valid-vote denominator.</summary>
    public ElectionAnalyticsFilter RequireOffice()
    {
        if (OfficeId is null)
        {
            throw new ArgumentException(
                "An office is required to compute vote shares.", nameof(OfficeId));
        }

        if (CandidateId is not null && CandidateIdentityId is not null)
        {
            throw new ArgumentException(
                "Filter by either a candidate or an identity, not both.", nameof(CandidateId));
        }

        return this;
    }

    /// <summary>Requires exactly one candidate or identity for candidate-centric analyses.</summary>
    public ElectionAnalyticsFilter RequireCandidate()
    {
        RequireOffice();

        if (CandidateId is null && CandidateIdentityId is null)
        {
            throw new ArgumentException(
                "A candidate or candidate identity is required.", nameof(CandidateId));
        }

        return this;
    }

    /// <summary>Requires a single round so results of different turns can never be aggregated.</summary>
    public ElectionAnalyticsFilter RequireRound()
    {
        if (Round is null)
        {
            throw new ArgumentException(
                "An election round is required for this analysis.", nameof(Round));
        }

        return this;
    }

    /// <summary>Requires office, round and a bounded election scope.</summary>
    /// <remarks>
    /// An office and a round fix the contest; a year or an election bounds the scan. Without the
    /// bound the same office and round would silently aggregate every election in the database.
    /// </remarks>
    public ElectionAnalyticsFilter RequireContest()
    {
        RequireOffice().RequireRound();

        if (Year is null && ElectionId is null)
        {
            throw new ArgumentException(
                "Either an election year or an election identifier is required.", nameof(Year));
        }

        return this;
    }

    /// <summary>Requires one election identifier for round-to-round comparisons.</summary>
    public ElectionAnalyticsFilter RequireElection()
    {
        if (ElectionId is null)
        {
            throw new ArgumentException(
                "An election identifier is required for this analysis.", nameof(ElectionId));
        }

        return this;
    }
}
