namespace Bravo.Domain.Electoral.Models;

public sealed record TsePartyReference(
    int Number,
    string Acronym,
    string Name
);
