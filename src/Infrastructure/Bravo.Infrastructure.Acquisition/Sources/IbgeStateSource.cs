using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Bravo.Application.Importing.Contracts;
using Bravo.Application.Importing.Exceptions;
using Bravo.Application.Importing.Models;
using Bravo.Domain.Geography.Entities;
using Bravo.Domain.Importing.Enums;
using Bravo.Infrastructure.Acquisition.Models;
using Bravo.Infrastructure.Acquisition.RateLimiting;
using Microsoft.Extensions.Logging;

namespace Bravo.Infrastructure.Acquisition.Sources;

public sealed class IbgeStateSource(
    IHttpClientFactory httpClientFactory,
    IbgeRateLimiter rateLimiter,
    ILogger<IbgeStateSource> logger) : IImportSource<State>
{
    public const string ImportType = "IbgeStates";

    private const string RequestUri = "api/v1/localidades/estados?orderBy=nome";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ImportSourceBatch<State>> ReadBatchAsync(
        string? cursor,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (cursor is not null)
        {
            return new ImportSourceBatch<State>([], [], null, HasMore: false);
        }

        var accepted = new List<State>();
        var rejected = new List<ImportRejectedRecord>();
        var position = 0;

        await foreach (var state in StreamStatesAsync(cancellationToken))
        {
            if (state is null)
            {
                rejected.Add(new ImportRejectedRecord(
                    $"ibge:states:{position}",
                    null,
                    ImportErrorCategory.Validation,
                    "The IBGE state record is missing required fields."));
            }
            else
            {
                accepted.Add(state);
            }

            position++;
        }

        return new ImportSourceBatch<State>(accepted, rejected, null, HasMore: false);
    }

    private async IAsyncEnumerable<State?> StreamStatesAsync(
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

        await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<IbgeEstadoDto>(
                           stream,
                           JsonOptions,
                           cancellationToken))
        {
            if (item is null)
            {
                yield return null;
                continue;
            }

            var region = item.Region?.Sail ?? item.Region?.Name;

            if (string.IsNullOrWhiteSpace(item.Sail) || string.IsNullOrWhiteSpace(region))
            {
                logger.LogWarning(
                    "Discarding IBGE state {StateId} because required fields are missing.",
                    item.Id);

                yield return null;
                continue;
            }

            yield return State.Create(item.Id, item.Sail, item.Name, region);
        }
    }
}
