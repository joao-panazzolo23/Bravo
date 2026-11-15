namespace Bravo.Application.Acquisition.Providers;

public record TseDatasetInfo(
    TseDatasetKind Kind,
    string DatasetIdentifier,
    string DisplayName,
    Uri SourceUri,
    string Format,
    int? Year,
    string? Version
);

public record TseElectionIdentity(
    string TseCode,
    int Year,
    int TypeCode,
    string TypeName,
    string Name,
    DateTime ElectionDate,
    int Round);

public record TseElectionMetadataRecord(
    string TseCode,
    int Year,
    int TypeCode,
    string TypeName,
    string Name,
    DateTime ElectionDate,
    int Round,
    IReadOnlyList<TseContestRecord> Contests);

public record TseContestRecord(
    int OfficeCode,
    string OfficeName,
    string? StateAbbreviation);

public record TseCandidateRecord(
    string ElectionTseCode,
    int ElectionYear,
    string ElectionName,
    DateTime ElectionDate,
    int Round,
    int TypeCode,
    string TypeName,
    string TseSequential,
    string Number,
    string Name,
    string BallotName,
    int OfficeCode,
    string OfficeName,
    int PartyNumber,
    string PartyAcronym,
    string PartyName,
    string StateAbbreviation,
    int? SituationCode,
    string? SituationDescription,
    DateOnly? BirthDate);

public record TsePollingSectionResultRecord(
    TseElectionIdentity Election,
    string StateAbbreviation,
    string MunicipalityTseCode,
    string ZoneNumber,
    string SectionNumber,
    string PollingPlaceCode,
    string? PollingPlaceName,
    string? PollingPlaceAddress,
    int OfficeCode,
    string OfficeName,
    string VotableNumber,
    string VotableName,
    string? CandidateSequential,
    int Votes,
    bool IsBlank,
    bool IsNull);

public record TsePollingSectionStatisticsRecord(
    TseElectionIdentity Election,
    string StateAbbreviation,
    string MunicipalityTseCode,
    string ZoneNumber,
    string SectionNumber,
    string PollingPlaceCode,
    string? PollingPlaceName,
    string? PollingPlaceAddress,
    int OfficeCode,
    string OfficeName,
    int? EligibleVoters,
    int? Attendees,
    int? Absentees,
    int? NominalVotes,
    int? BlankVotes,
    int? NullVotes,
    int? PartyVotes
);
