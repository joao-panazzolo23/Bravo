using Bravo.Application.Logging;
using Bravo.Domain.Importing.Enums;
using System.Net;
using System.Text.Json;
using Bravo.Application.Importing.Exceptions;
using Bravo.Domain.Geography.Repositories;
using Bravo.Domain.Importing.Models;
using Bravo.Domain.Importing.Services;
using Bravo.Infrastructure.Acquisition.Models;
using Bravo.Infrastructure.Acquisition.Options;
using Bravo.Infrastructure.Acquisition.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bravo.Infrastructure.Acquisition.Importing;

/// <summary>
/// Imports IBGE states and then their municipalities.
/// </summary>
/// <remarks>
/// The stages are strictly ordered: a municipality references its state by IBGE code, so the
/// state upsert must be committed first. Both stages stream JSON and write in batches; a single
/// archive or request failure fails the job, which is then retried from the beginning.
/// </remarks>
public sealed class IbgeImportSource(
    IHttpClientFactory httpClientFactory,
    IbgeRateLimiter rateLimiter,
    IGeographyRepository geography,
    IOptions<IbgeOptions> options,
    ILogger<IbgeImportSource> logger) : IImportSource
{
    public const string SourceKey = "IBGE";

    private const string StatesRequestUri = "api/v1/localidades/estados?orderBy=nome";
    private const string MunicipalitiesRequestUri = "api/v1/localidades/municipios?orderBy=nome";
    private const int OperationCount = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string Key => SourceKey;

    public async Task ExecuteAsync(
        ImportExecutionContext context,
        CancellationToken cancellationToken)
    {
        context.Report(ImportStage.References, OperationCount, 0, 0, 0, 0, "IBGE states");
        var states = await ImportStatesAsync(context.BatchSize, cancellationToken);
        context.Report(ImportStage.References, OperationCount, 1, 0, states, 0, "IBGE municipalities");

        var municipalities = await ImportMunicipalitiesAsync(context.BatchSize, cancellationToken);
        context.Report(ImportStage.Persisting, OperationCount, 2, 0, states + municipalities, 0, null);

        PersistenceLog.IbgeImportCompleted(logger, states, municipalities);
    }

    private async Task<int> ImportStatesAsync(int batchSize, CancellationToken cancellationToken)
    {
        var buffer = new List<Domain.Geography.Entities.State>(batchSize);
        var imported = 0;
        var position = 0;

        await foreach (var state in StreamStatesAsync(cancellationToken))
        {
            position++;

            if (state is null)
                continue;

            buffer.Add(state);

            if (buffer.Count < batchSize)
                continue;

            imported += await geography.UpsertStatesAsync(buffer.ToArray(), cancellationToken);
            buffer.Clear();
        }

        if (buffer.Count > 0)
            imported += await geography.UpsertStatesAsync(buffer.ToArray(), cancellationToken);

        if (position == 0)
            throw new ImportPermanentException("IBGE returned no states.");

        return imported;
    }

    private async Task<int> ImportMunicipalitiesAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        var buffer = new List<MunicipalityUpsert>(batchSize);
        var imported = 0;

        await foreach (var municipality in StreamMunicipalitiesAsync(cancellationToken))
        {
            buffer.Add(municipality);

            if (buffer.Count < batchSize)
                continue;

            imported += await PersistMunicipalitiesAsync(buffer, cancellationToken);
            buffer.Clear();
        }

        if (buffer.Count > 0)
            imported += await PersistMunicipalitiesAsync(buffer, cancellationToken);

        return imported;
    }

    private async Task<int> PersistMunicipalitiesAsync(
        List<MunicipalityUpsert> buffer,
        CancellationToken cancellationToken)
    {
        var persisted = await geography.UpsertMunicipalitiesAsync(buffer.ToArray(), cancellationToken);

        if (persisted < buffer.Count)
        {
            throw new ImportPermanentException(
                $"Persisted {persisted} of {buffer.Count} IBGE municipalities; " +
                "some states are missing and must be imported first.");
        }

        return persisted;
    }

    private async IAsyncEnumerable<Domain.Geography.Entities.State?> StreamStatesAsync(
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

            yield return Domain.Geography.Entities.State.Create(item.Id, item.Sail, item.Name, region);
        }
    }

    private async IAsyncEnumerable<MunicipalityUpsert> StreamMunicipalitiesAsync(
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
