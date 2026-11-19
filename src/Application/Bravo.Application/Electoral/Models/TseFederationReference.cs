namespace Bravo.Application.Electoral.Models;

public sealed record TseFederationReference(
    int Number,
    string Name,
    string Acronym,
    string? Composition
);
