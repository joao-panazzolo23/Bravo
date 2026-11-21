using System.Runtime.CompilerServices;
using Bravo.Application.Logging;
using Bravo.Application.Importing.Sources.Ibge;
using Bravo.Domain.Geography.Entities;
using Bravo.Domain.Geography.Repositories;
using Bravo.Infrastructure.Ibge.Clients;
using Bravo.Infrastructure.Ibge.Models;
using Microsoft.Extensions.Logging;

namespace Bravo.Infrastructure.Ibge.Importing;

/// <summary>
/// Reads IBGE's reference collections and maps them to domain types.
/// </summary>
/// <remarks>
/// Transport lives in <see cref="IbgeHttpClient"/>; this type only validates the payload and
/// decides how each record maps. Batching, ordering and persistence are orchestrated by the
/// application source, so the acquisition layer never decides how data is stored.
/// </remarks>
public sealed class IbgeReader(
    IbgeHttpClient client,
    ILogger<IbgeReader> logger
) : IIbgeReader
{
    public async IAsyncEnumerable<State?> StreamStatesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in client.StreamStatesAsync(cancellationToken))
        {
            if (item is null)
                continue;

            var region = item.Region?.Sail ?? item.Region?.Name;

            if (string.IsNullOrWhiteSpace(item.Sail) || string.IsNullOrWhiteSpace(region))
            {
                ValidationLog.IbgeRecordDiscarded(
                    logger, "state", item.Id, "required fields are missing");

                yield return null;
                continue;
            }

            yield return State.Create(item.Id, item.Sail, item.Name, region);
        }
    }

    public async IAsyncEnumerable<MunicipalityUpsert> StreamMunicipalitiesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in client.StreamMunicipalitiesAsync(cancellationToken))
        {
            if (item is null)
                continue;

            var stateCode = GetStateCode(item);

            if (stateCode is null)
            {
                ValidationLog.IbgeRecordDiscarded(
                    logger, "municipality", item.Id, "state could not be resolved");

                continue;
            }

            yield return new MunicipalityUpsert(item.Id, item.Nome, stateCode.Value);
        }
    }

    private static int? GetStateCode(IbgeMunicipalityDto item)
        => item.MicroRegion?.MesoRegion?.FederativeUnit?.Id
           ?? item.ImmediateRegion?.IntermediateRegion?.FederativeUnit?.Id;
}
