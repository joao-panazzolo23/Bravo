namespace Bravo.Domain.Electoral.Analytics;

/// <summary>
/// Vote composition of one geography in one round, derived from stored section results.
/// </summary>
/// <remarks>
/// Every field except the participation ones is a sum of stored source facts. Participation
/// (eligible voters, attendance and abstention) is <see langword="null"/> because the schema keeps
/// no eligible-voter source: <c>ElectionStatistics</c> was dropped, and reconstructing attendance
/// from votes cast would be an inference, not a measurement. Percentages that would divide by
/// eligible voters are therefore absent, while blank and null shares use the votes cast.
/// </remarks>
public sealed record TurnoutComposition(
    int Year,
    Guid ElectionId,
    string OfficeName,
    short Round,
    VoteCompositionLevel Level,
    Guid? StateId,
    string? StateAbbreviation,
    Guid? MunicipalityId,
    string? MunicipalityName,
    Guid? ZoneId,
    int? TseZoneNumber,
    Guid? SectionId,
    int? TseSectionNumber,
    long VotesCast,
    long ValidVotes,
    long BlankVotes,
    long NullVotes,
    long? EligibleVoters,
    long? AttendingVoters,
    long? AbsenteeVoters,
    decimal? TurnoutPercentage,
    decimal? AbsenteeismPercentage,
    decimal? BlankVotePercentage,
    decimal? NullVotePercentage);
