namespace Bravo.Application.Electoral.Models;

public sealed record TseElectionReference(
    int Code,
    int Year,
    int TypeCode,
    string TypeName,
    string Description,
    DateOnly? Date,
    short Round
);
