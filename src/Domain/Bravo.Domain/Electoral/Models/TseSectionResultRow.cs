namespace Bravo.Domain.Electoral.Models;

public sealed record TseSectionResultRow(
    int Year,
    short Round,
    int ElectionCode,
    string StateAcronym,
    string TseMunicipalityCode,
    string MunicipalityName,
    int ZoneNumber,
    int SectionNumber,
    int OfficeCode,
    int BallotNumber,
    string BallotName,
    long Votes,
    int PollingPlaceNumber,
    string? CandidateSequential
);
