namespace Bravo.Application.Imports.Ibge.Models;

/// <summary>
/// One state as the IBGE source published it, before validation.
/// </summary>
/// <remarks>
/// Every attribute is optional on purpose: the reader's job is to project the wire format onto a
/// shape, and deciding whether that shape is usable is an application decision. A record that
/// cannot become a <c>State</c> is discarded here with a diagnostic rather than silently dropped by
/// the transport.
/// </remarks>
public sealed record IbgeStateRecord(
    int IbgeCode,
    string? Abbreviation,
    string? Name,
    string? Region);

/// <summary>
/// One municipality as the IBGE source published it, before validation.
/// </summary>
/// <remarks>
/// <paramref name="StateIbgeCode"/> is nullable because IBGE publishes municipalities whose region
/// tree does not lead to a federative unit. Resolving the code is a property of the wire format;
/// treating the absence as a rejected record is an application policy.
/// </remarks>
public sealed record IbgeMunicipalityRecord(
    int IbgeCode,
    string? Name,
    int? StateIbgeCode);
