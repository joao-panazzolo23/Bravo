using System.Runtime.CompilerServices;
using System.Text.Json;
using Bravo.Application.Acquisition.Providers;
using Bravo.Infrastructure.Acquisition.RateLimiting;
using Microsoft.Extensions.Logging;
using Paramore.Fences;

namespace Bravo.Infrastructure.Acquisition.Providers;

public sealed class IbgeProvider(
    IHttpClientFactory httpClientFactory,
    IbgeRateLimiter rateLimiter,
    ResiliencePipeline<HttpResponseMessage> pipeline,
    ILogger<IbgeProvider> logger
) : IIbgeProvider
{
    private const string ClientName = "ibge";
    private const string StatesPath = "api/v1/localidades/estados?orderBy=nome";
    private const string MunicipalitiesPath = "api/v1/localidades/municipios?orderBy=nome";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Task<IbgeDatasetInfo> GetDatasetInfo(CancellationToken ct = default)
    {
        var info = new IbgeDatasetInfo(
            StatesUrl: StatesPath,
            MunicipalitiesUrl: MunicipalitiesPath,
            Version: DateTime.UtcNow.ToString("yyyyMMdd"),
            Format: "JSON"
        );
        return Task.FromResult(info);
    }

    public async IAsyncEnumerable<IbgeStateRecord> StreamStates([EnumeratorCancellation] CancellationToken ct = default)
    {
        using var response = await SendAsync(StatesPath, ct);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);

        await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<IbgeEstadoDto>(stream, JsonOptions, ct))
        {
            if (item is null) continue;

            logger.LogInformation($"Processing State: {item.Name}");

            yield return new IbgeStateRecord(
                item.Id,
                item.Sail,
                item.Name,
                item.Region?.Sail ?? item.Region?.Name ?? string.Empty
            );
        }
    }

    public async IAsyncEnumerable<IbgeMunicipalityRecord> StreamMunicipalities([EnumeratorCancellation] CancellationToken ct = default)
    {
        using var response = await SendAsync(MunicipalitiesPath, ct);

        await using var stream = await response.Content.ReadAsStreamAsync(ct);

        await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<IbgeTownDto>(stream, JsonOptions, ct))
        {
            if (item is null) continue;
            yield return new IbgeMunicipalityRecord(
                item.Id,
                item.Nome,
                item.FederativeUnit?.Id ?? 0
            );
        }
    }

    private async Task<HttpResponseMessage> SendAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        using var lease = await rateLimiter.AcquireAsync(cancellationToken);

        var client = httpClientFactory.CreateClient(ClientName);

        var response = await pipeline.ExecuteAsync(
            token => new ValueTask<HttpResponseMessage>(
                client.GetAsync(relativeUrl, HttpCompletionOption.ResponseHeadersRead, token)),
            cancellationToken);

        return response;
    }
}
