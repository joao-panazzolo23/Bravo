using Bravo.Application.Acquisition.Services;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Bravo.Application.Acquisition.Providers;
using Bravo.Infrastructure.Acquisition.Http;
using Bravo.Application.Acquisition.Options;

namespace Bravo.Infrastructure.Acquisition.Providers;

/// <summary>
/// IBGE locality provider. The official API returns JSON arrays of states and
/// municipalities; both are streamed from a temporary file with
/// <see cref="JsonSerializer.DeserializeAsyncEnumerable{TValue}"/> so the dataset is never
/// materialized in memory.
/// </summary>
public sealed class IbgeProvider(DatasetFetcher fetcher, ProviderOptions policy) : IIbgeProvider
{
    private const string StatesPath = "api/v1/localidades/estados?orderBy=nome";
    private const string MunicipalitiesPath = "api/v1/localidades/municipios?orderBy=nome";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private Uri BaseUri => new(policy.BaseAddress);

    public Task<IbgeDatasetInfo> GetDatasetInfo(CancellationToken ct = default)
    {
        var info = new IbgeDatasetInfo(
            StatesUrl: new Uri(BaseUri, StatesPath).ToString(),
            MunicipalitiesUrl: new Uri(BaseUri, MunicipalitiesPath).ToString(),
            Version: DateTime.UtcNow.ToString("yyyyMMdd"),
            Format: "JSON");

        return Task.FromResult(info);
    }

    public Uri GetSourceUri(DatasetScope scope) => scope switch
    {
        DatasetScope.States => new Uri(BaseUri, StatesPath),
        DatasetScope.Municipalities => new Uri(BaseUri, MunicipalitiesPath),
        _ => throw new ArgumentOutOfRangeException(nameof(scope))
    };

    public Task<CachedDownload> DownloadStates(
        Guid executionId,
        ConditionalRequest? conditional = null,
        CancellationToken ct = default) =>
        DownloadRequired(executionId, DatasetScope.States, conditional, ct);

    public Task<CachedDownload> DownloadMunicipalities(
        Guid executionId,
        ConditionalRequest? conditional = null,
        CancellationToken ct = default) =>
        DownloadRequired(executionId, DatasetScope.Municipalities, conditional, ct);

    private async Task<CachedDownload> DownloadRequired(
        Guid executionId,
        DatasetScope scope,
        ConditionalRequest? conditional,
        CancellationToken ct)
    {
        var key = scope == DatasetScope.States ? "ibge-states" : "ibge-municipalities";
        var downloaded = await fetcher.DownloadAsync(executionId, key, GetSourceUri(scope), conditional, ct);

        if (downloaded is null)
        {
            // A 304 means the previously acquired file is still current. IBGE does not
            // currently serve such validators, so reaching this branch indicates that a
            // conditional request was sent when no cached file is available.
            throw new InvalidOperationException($"IBGE dataset '{key}' was reported as unmodified but no local copy exists.");
        }

        return downloaded;
    }

    public async IAsyncEnumerable<ParsedRecord<IbgeStateRecord>> StreamStates(
        string filePath,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var stream = OpenRead(filePath);

        await foreach (var item in JsonSerializer
                           .DeserializeAsyncEnumerable<IbgeEstadoDto>(stream, JsonOptions, ct)
                           .ConfigureAwait(false))
        {
            if (item is null)
            {
                continue;
            }

            if (item.Id <= 0 || string.IsNullOrWhiteSpace(item.Name))
            {
                yield return new ParsedRecord<IbgeStateRecord>(null, $"state id={item.Id}");
                continue;
            }

            yield return new ParsedRecord<IbgeStateRecord>(
                new IbgeStateRecord(
                    item.Id,
                    item.Sail,
                    item.Name,
                    item.Region?.Sail ?? item.Region?.Name ?? string.Empty),
                $"state {item.Id}");
        }
    }

    public async IAsyncEnumerable<ParsedRecord<IbgeMunicipalityRecord>> StreamMunicipalities(
        string filePath,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var stream = OpenRead(filePath);

        await foreach (var item in JsonSerializer
                           .DeserializeAsyncEnumerable<IbgeTownDto>(stream, JsonOptions, ct)
                           .ConfigureAwait(false))
        {
            if (item is null)
            {
                continue;
            }

            var state = item.ResolveState();
            if (item.Id <= 0 || string.IsNullOrWhiteSpace(item.Nome) || state?.Id is null or <= 0)
            {
                yield return new ParsedRecord<IbgeMunicipalityRecord>(null, $"municipality id={item.Id}");
                continue;
            }

            yield return new ParsedRecord<IbgeMunicipalityRecord>(
                new IbgeMunicipalityRecord(item.Id, item.Nome, state.Id),
                $"municipality {item.Id}");
        }
    }

    private static FileStream OpenRead(string filePath) => new(
        filePath,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        81920,
        FileOptions.Asynchronous | FileOptions.SequentialScan);
}
