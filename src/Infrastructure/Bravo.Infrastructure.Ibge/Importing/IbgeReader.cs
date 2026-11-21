using System.Net;
using System.Text.Json;
using Bravo.Application.Importing.Exceptions;
using Bravo.Application.Importing.Sources.Ibge;
using Bravo.Application.Logging;
using Bravo.Domain.Geography.Entities;
using Bravo.Domain.Geography.Repositories;
using Bravo.Infrastructure.Ibge.Models;
using Bravo.Infrastructure.Ibge.RateLimiting;
using Microsoft.Extensions.Logging;

namespace Bravo.Infrastructure.Ibge.Importing;

/// <summary>
/// Reads IBGE's reference collections over HTTP as a stream.
/// </summary>
/// <remarks>
/// This type owns transport only: rate limiting, request lifecycle, status handling and JSON
/// decoding. Batching, ordering and persistence are orchestrated by the application source, so
/// the acquisition layer never decides how data is stored.
/// </remarks>
public sealed class IbgeReader(
    IHttpClientFactory httpClientFactory,
    IbgeRateLimiter rateLimiter,
    ILogger<IbgeReader> logger) : IIbgeReader
{
    private const string StatesRequestUri = "api/v1/localidades/estados?orderBy=nome";
    private const string MunicipalitiesRequestUri = "api/v1/localidades/municipios?orderBy=nome";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async IAsyncEnumerable<State?> StreamStatesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var lease = await rateLimiter.AcquireAsync(cancellationToken);

        if (!lease.IsAcquired)
            throw new ImportTransientException("The IBGE request rate limit is exhausted.");

        var client = httpClientFactory.CreateClient(IbgeHttpClient.Name);
        using var response = await client.GetAsync(
            StatesRequestUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<IbgeEstadoDto>(
                           stream, JsonOptions, cancellationToken))
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
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var lease = await rateLimiter.AcquireAsync(cancellationToken);

        if (!lease.IsAcquired)
            throw new ImportTransientException("The IBGE request rate limit is exhausted.");

        var client = httpClientFactory.CreateClient(IbgeHttpClient.Name);
        using var response = await client.GetAsync(
            MunicipalitiesRequestUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<IbgeMunicipalityDto>(
                           stream, JsonOptions, cancellationToken))
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

    private static Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new ImportTransientException(
                "IBGE rate limit was reached.",
                response.Headers.RetryAfter?.Delta);
        }

        if ((int)response.StatusCode >= 500)
            throw new ImportTransientException($"IBGE returned {(int)response.StatusCode}.");

        if (!response.IsSuccessStatusCode)
        {
            throw new ImportPermanentException(
                $"IBGE returned {(int)response.StatusCode} for '{response.RequestMessage?.RequestUri}'.");
        }

        return Task.CompletedTask;
    }

    private static int? GetStateCode(IbgeMunicipalityDto item)
        => item.MicroRegion?.MesoRegion?.FederativeUnit?.Id
           ?? item.ImmediateRegion?.IntermediateRegion?.FederativeUnit?.Id;
}
