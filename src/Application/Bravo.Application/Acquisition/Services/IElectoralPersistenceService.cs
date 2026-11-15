using Bravo.Application.Acquisition.Providers;

namespace Bravo.Application.Acquisition.Services;

/// <summary>
/// Bulk persistence of the electoral reference model (elections, rounds, contests, parties,
/// candidates, zones, places and sections). Implementations use set-based SQL.
/// </summary>
public interface IElectoralPersistenceService
{
    /// <summary>
    /// Ensures an election exists and returns its identifier.
    /// </summary>
    Task<Guid> EnsureElection(
        string tseCode,
        int year,
        int typeCode,
        string typeName,
        string name,
        DateTime electionDate,
        CancellationToken ct = default);

    /// <summary>
    /// Ensures a round exists and returns its identifier.
    /// </summary>
    Task<Guid> EnsureElectionRound(Guid electionId, int number, CancellationToken ct = default);

    /// <summary>
    /// Ensures a contest exists and returns its identifier. State is resolved by abbreviation.
    /// </summary>
    Task<Guid> EnsureContest(
        Guid electionRoundId,
        int officeCode,
        string officeName,
        string? stateAbbreviation,
        CancellationToken ct = default);

    /// <summary>
    /// Ensures parties exist and returns a lookup by party number.
    /// </summary>
    Task<IReadOnlyDictionary<int, Guid>> EnsureParties(
        IReadOnlyList<PartyReference> parties,
        CancellationToken ct = default);

    /// <summary>
    /// Upserts candidates. The natural key is the election plus the TSE sequential code.
    /// </summary>
    Task<PersistenceResult> UpsertCandidates(
        Guid electionId,
        IReadOnlyList<CandidateReferenceRecord> batch,
        IReadOnlyDictionary<int, Guid> partyIds,
        IReadOnlyDictionary<string, Guid> stateIds,
        CancellationToken ct = default);

    /// <summary>
    /// Resolves state and municipality identifiers used by electoral records.
    /// </summary>
    Task<GeographicLookup> GetGeographicLookup(CancellationToken ct = default);
}

public record PartyReference(int Number, string Acronym, string Name);

public record CandidateReferenceRecord(
    string TseSequential,
    string Number,
    string Name,
    string BallotName,
    int OfficeCode,
    string OfficeName,
    int PartyNumber,
    string StateAbbreviation,
    int? SituationCode,
    string? SituationDescription,
    DateOnly? BirthDate);

public record GeographicLookup(
    IReadOnlyDictionary<string, Guid> StatesByAbbreviation,
    IReadOnlyDictionary<string, Guid> MunicipalitiesByTseCode);
