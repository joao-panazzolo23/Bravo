using System.Runtime.CompilerServices;
using Bravo.Application.Imports.Ibge.Abstractions;
using Bravo.Application.Imports.Ibge.Models;
using Bravo.Infrastructure.Ibge.Clients;
using Bravo.Infrastructure.Ibge.Models;

namespace Bravo.Infrastructure.Ibge.Importing;

/// <summary>
/// Reads IBGE's reference collections and projects them onto the application's import records.
/// </summary>
/// <remarks>
/// Transport lives in <see cref="IbgeHttpClient"/>; this type only maps the wire DTO onto the
/// application record. It deliberately does not decide whether a record is valid or build domain
/// entities: an absent abbreviation or region is preserved as a null so the application can reject
/// it with a diagnostic, which is the same policy the TSE import applies.
/// </remarks>
public sealed class IbgeReader(IbgeHttpClient client) : IIbgeReader
{
    public async IAsyncEnumerable<IbgeStateRecord> StreamStatesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in client.StreamStatesAsync(cancellationToken))
        {
            if (item is null)
                continue;

            // IBGE exposes the region acronym under either name depending on the endpoint; the
            // reader keeps both candidates and lets the application decide what is usable.
            var region = item.Region?.Sail ?? item.Region?.Name;

            yield return new IbgeStateRecord(item.Id, item.Sail, item.Name, region);
        }
    }

    public async IAsyncEnumerable<IbgeMunicipalityRecord> StreamMunicipalitiesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in client.StreamMunicipalitiesAsync(cancellationToken))
        {
            if (item is null)
                continue;

            yield return new IbgeMunicipalityRecord(item.Id, item.Nome, GetStateCode(item));
        }
    }

    /// <summary>
    /// Resolves the federative unit id from either region tree. This is wire-format knowledge: the
    /// municipality DTO nests its state under a different path depending on which IBGE endpoint
    /// produced the payload.
    /// </summary>
    private static int? GetStateCode(IbgeMunicipalityDto item)
        => item.MicroRegion?.MesoRegion?.FederativeUnit?.Id
           ?? item.ImmediateRegion?.IntermediateRegion?.FederativeUnit?.Id;
}
