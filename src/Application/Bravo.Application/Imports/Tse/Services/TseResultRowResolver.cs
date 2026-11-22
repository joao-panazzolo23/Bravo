using Bravo.Application.Imports.Tse.Models;
using Bravo.Domain.Electoral.Enums;
using Bravo.Domain.Electoral.Models;
using Bravo.Domain.Electoral.Services;

namespace Bravo.Application.Imports.Tse.Services;

/// <summary>
/// The canonical identifiers of one accumulated result row.
/// </summary>
public readonly record struct TseResultKey(
    Guid RoundId,
    Guid SectionId,
    Guid OfficeId,
    VoteType Type,
    Guid? CandidateId,
    Guid? PartyId,
    int Ballot);

/// <summary>
/// Why a source row could not be turned into a result key.
/// </summary>
/// <remarks>
/// The distinction matters for progress reporting: a row the source itself cannot resolve is a
/// rejection, not a silent success, and it must be counted so the job can report
/// <c>CompletedWithErrors</c> instead of pretending everything was imported.
/// </remarks>
public enum TseResultRejection
{
    /// <summary>The row was resolved.</summary>
    None,

    /// <summary>The TSE municipality code has no IBGE mapping.</summary>
    Municipality,

    /// <summary>The zone or section is not part of the prepared structure.</summary>
    Structure,

    /// <summary>The office code is not imported.</summary>
    Office,

    /// <summary>The election or round code is not imported.</summary>
    Election,

    /// <summary>A nominal vote references a candidate that was not imported.</summary>
    Candidate,

    /// <summary>A party vote references a party that was not imported.</summary>
    Party
}

/// <summary>
/// Resolves a TSE section-result row into the identifiers an <c>ElectionResult</c> needs.
/// </summary>
/// <remarks>
/// This used to be a mutable struct nested inside the orchestration class, which made it
/// impossible to test without running a whole import. It holds only preloaded maps, so it is
/// read-only after construction and can be shared by the parser and any test that wants to
/// exercise one row.
///
/// Resolution is deliberately pure: no database access, no logging and no aggregation. That is what
/// lets <see cref="TseResultImporter"/> stream millions of rows through it.
/// </remarks>
public sealed class TseResultRowResolver(
    Guid stateId,
    IReadOnlyDictionary<string, Guid?> municipalityIds,
    TseStructureIds structureIds,
    IReadOnlyDictionary<int, Guid> officeIds,
    IReadOnlyDictionary<int, Guid> partyByNumber,
    TseElectionLookup electionLookup,
    IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, Guid>> candidateIds)
{
    public bool TryResolve(
        TseSectionResultRow row,
        out TseResultKey key,
        out TseResultRejection rejection)
    {
        key = default;
        rejection = TseResultRejection.None;

        if (!municipalityIds.TryGetValue(row.TseMunicipalityCode, out var municipalityId)
            || municipalityId is null)
        {
            rejection = TseResultRejection.Municipality;
            return false;
        }

        if (!structureIds.ZoneIds.TryGetValue((stateId, row.ZoneNumber), out var zoneId))
        {
            rejection = TseResultRejection.Structure;
            return false;
        }

        if (!structureIds.SectionIds.TryGetValue(
                (municipalityId.Value, zoneId, row.SectionNumber), out var sectionId))
        {
            rejection = TseResultRejection.Structure;
            return false;
        }

        if (!officeIds.TryGetValue(row.OfficeCode, out var officeId))
        {
            rejection = TseResultRejection.Office;
            return false;
        }

        if (!electionLookup.TryGetElectionId(row.ElectionCode, out var electionId))
        {
            rejection = TseResultRejection.Election;
            return false;
        }

        if (!electionLookup.TryGetRoundId(row.ElectionCode, row.Round, out var roundId))
        {
            rejection = TseResultRejection.Election;
            return false;
        }

        var voteType = TseVoteClassifier.Classify(row.BallotNumber, row.CandidateSequential);

        if (voteType == VoteType.Nominal)
        {
            if (candidateIds.TryGetValue(electionId, out var bySequential)
                && bySequential.TryGetValue(row.CandidateSequential!, out var candidateId))
            {
                key = new TseResultKey(
                    roundId, sectionId, officeId, voteType, candidateId, null, row.BallotNumber);
                return true;
            }

            rejection = TseResultRejection.Candidate;
            return false;
        }

        if (voteType == VoteType.Party)
        {
            if (partyByNumber.TryGetValue(row.BallotNumber, out var partyId))
            {
                key = new TseResultKey(
                    roundId, sectionId, officeId, voteType, null, partyId, row.BallotNumber);
                return true;
            }

            rejection = TseResultRejection.Party;
            return false;
        }

        key = new TseResultKey(roundId, sectionId, officeId, voteType, null, null, row.BallotNumber);
        return true;
    }
}
