using Bravo.Domain.Electoral.Enums;

namespace Bravo.Application.Imports.Tse.Models;

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
    int Ballot
);
