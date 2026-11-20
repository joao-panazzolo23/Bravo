namespace Bravo.Domain.Geography.Repositories;

/// <summary>
/// A municipality as published by a source. The state is referenced by its source code
/// because the identifier of the stored state row is not known before the upsert.
/// </summary>
public sealed record MunicipalityUpsert(
    int IbgeCode,
    string Name,
    int StateIbgeCode);
