using Bravo.Domain.Geography.Entities;
using Bravo.Domain.Geography.Repositories;

namespace Bravo.Application.Importing.Sources.Ibge;

/// <summary>
/// Streams the IBGE reference collections.
/// </summary>
/// <remarks>
/// Acquisition is an infrastructure concern (HTTP, rate limiting, JSON decoding), while deciding
/// how to batch and persist the rows belongs to the application. This boundary lets the
/// application own the import orchestration without depending on the transport.
///
/// Both methods stream: the municipality collection is thousands of rows and must never be
/// materialised in full.
/// </remarks>
public interface IIbgeReader
{
    IAsyncEnumerable<State?> StreamStatesAsync(CancellationToken cancellationToken = default);

    IAsyncEnumerable<MunicipalityUpsert> StreamMunicipalitiesAsync(
        CancellationToken cancellationToken = default);
}
