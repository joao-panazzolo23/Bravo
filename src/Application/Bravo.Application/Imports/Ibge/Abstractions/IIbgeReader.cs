using Bravo.Application.Imports.Ibge.Models;

namespace Bravo.Application.Imports.Ibge.Abstractions;

/// <summary>
/// Streams the IBGE reference collections.
/// </summary>
/// <remarks>
/// Acquisition is an infrastructure concern (HTTP, rate limiting, JSON decoding), while deciding
/// which records are valid and how to batch them belongs to the application. The reader therefore
/// returns source records with nullable attributes; it never builds a domain entity.
///
/// Both methods stream: the municipality collection is thousands of rows and must never be
/// materialised in full.
/// </remarks>
public interface IIbgeReader
{
    IAsyncEnumerable<IbgeStateRecord> StreamStatesAsync(CancellationToken cancellationToken = default);

    IAsyncEnumerable<IbgeMunicipalityRecord> StreamMunicipalitiesAsync(
        CancellationToken cancellationToken = default);
}
