namespace Bravo.Domain.Electoral.Models;

public sealed record TseFederationReference(
    int Number,
    string Name,
    string Acronym,
    string? Composition
);
