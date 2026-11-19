using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Bravo.Application.Acquisition.Providers;
using Bravo.Domain.Importing.Services;
using Bravo.Application.Importing.Exceptions;
using Bravo.Domain.Importing.Models;
using Bravo.Domain.Importing.Enums;
using Bravo.Infrastructure.Acquisition.Models;
using Bravo.Infrastructure.Acquisition.RateLimiting;
using Microsoft.Extensions.Logging;

namespace Bravo.Infrastructure.Acquisition.Sources;

///I think this is useless now
/// TODO: take a look later to remove
public sealed class IbgeMunicipalitySource(
    IHttpClientFactory httpClientFactory,
    IbgeRateLimiter rateLimiter,
    ILogger<IbgeMunicipalitySource> logger) : IImportSource<IbgeMunicipalityRecord>
{
    private const string RequestUri = "api/v1/localidades/municipios?orderBy=nome";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ImportSourceBatch<IbgeMunicipalityRecord>> ReadBatchAsync(
        string? cursor,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (cursor is not null)
        {
            return new ImportSourceBatch<IbgeMunicipalityRecord>([], [], null, HasMore: false);
        }

        var accepted = new List<IbgeMunicipalityRecord>();
        var rejected = new List<ImportRejectedRecord>();

        await foreach (var item in StreamMunicipalitiesAsync(cancellationToken))
        {
            var stateCode = GetStateCode(item);

            if (stateCode is null)
            {
                logger.LogWarning(
                    "Discarding IBGE municipality {MunicipalityId} because its state could not be resolved.",
                    item.Id);

                rejected.Add(
                    new ImportRejectedRecord(
                    $"ibge:municipalities:{item.Id}",
                    null,
                    ImportErrorCategory.Source,
                    "The state of the municipality could not be resolved.")
                );
            }
            else
            {
                accepted.Add(new IbgeMunicipalityRecord(item.Id, item.Nome, stateCode.Value));
            }
        }

        return new ImportSourceBatch<IbgeMunicipalityRecord>(accepted, rejected, null, HasMore: false);
    }

    private static int? GetStateCode(IbgeMunicipalityDto item)
        => item.MicroRegion?.MesoRegion?.FederativeUnit?.Id
           ?? item.ImmediateRegion?.IntermediateRegion?.FederativeUnit?.Id;

    private async IAsyncEnumerable<IbgeMunicipalityDto> StreamMunicipalitiesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var lease = await rateLimiter.AcquireAsync(cancellationToken);

        if (!lease.IsAcquired)
            throw new ImportTransientException("The IBGE request rate limit is exhausted.");

        var client = httpClientFactory.CreateClient(IbgeHttpClient.Name);
        using var response = await client.GetAsync(
            RequestUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new ImportTransientException(
                "IBGE rate limit was reached.",
                response.Headers.RetryAfter?.Delta);
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<IbgeMunicipalityDto>(
                           stream,
                           JsonOptions,
                           cancellationToken))
        {
            if (item is not null)
                yield return item;
        }
    }
}
