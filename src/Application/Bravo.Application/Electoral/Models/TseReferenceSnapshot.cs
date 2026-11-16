namespace Bravo.Application.Electoral.Models;

public sealed record TseElectionReference(
    int Code,
    int Year,
    int TypeCode,
    string TypeName,
    string Description,
    DateOnly? Date);

public sealed record TseOfficeReference(int Code, string Name);

public sealed record TsePartyReference(int Number, string Acronym, string Name);

public sealed record TseFederationReference(
    int Number,
    string Name,
    string Acronym,
    string? Composition);

public sealed record TseReferenceSnapshot(
    TseElectionReference? Election,
    IReadOnlyList<TseOfficeReference> Offices,
    IReadOnlyList<TsePartyReference> Parties,
    IReadOnlyList<TseFederationReference> Federations);
