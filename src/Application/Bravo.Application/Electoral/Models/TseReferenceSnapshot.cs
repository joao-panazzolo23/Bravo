namespace Bravo.Application.Electoral.Models;

public sealed record TseReferenceSnapshot(
    IReadOnlyList<TseElectionReference> Elections,
    IReadOnlyList<TseOfficeReference> Offices,
    IReadOnlyList<TsePartyReference> Parties,
    IReadOnlyList<TseFederationReference> Federations
);
