using System.Runtime.CompilerServices;
using System.Text.Json;
using Bravo.Application.Acquisition.Providers;
using Bravo.Infrastructure.Acquisition.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bravo.Infrastructure.Acquisition.Providers;

public sealed class IbgeProvider(
    IHttpClientFactory httpClientFactory,
    RateLimiting.IbgeRateLimiter rateLimiter,
    IOptions<IbgeOptions> options,
    ILogger<IbgeProvider> logger
) : IIbgeProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Task<IbgeDatasetInfo> GetDatasetInfo(CancellationToken ct = default)
    {
        var info = new IbgeDatasetInfo(
            StatesUrl: "api/v1/localidades/estados?orderBy=nome",
            MunicipalitiesUrl: "api/v1/localidades/municipios?orderBy=nome",
            Version: DateTime.UtcNow.ToString("yyyyMMdd"),
            Format: "JSON"
        );
        return Task.FromResult(info);
    }

    public async IAsyncEnumerable<IbgeStateRecord> StreamStates([EnumeratorCancellation] CancellationToken ct = default)
    {
        var response = await SendWithRetryAsync("api/v1/localidades/estados?orderBy=nome", ct);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);

        await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<IbgeEstadoDto>(stream, JsonOptions, ct))
        {
            if (item is null) continue;
            yield return new IbgeStateRecord(
                item.Id,
                item.Sigla,
                item.Nome,
                item.Regiao?.Sigla ?? item.Regiao?.Nome ?? string.Empty
            );
        }
    }

    public async IAsyncEnumerable<IbgeMunicipalityRecord> StreamMunicipalities([EnumeratorCancellation] CancellationToken ct = default)
    {
        var response = await SendWithRetryAsync("api/v1/localidades/municipios?orderBy=nome", ct);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);

        await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<IbgeMunicipioDto>(stream, JsonOptions, ct))
        {
            if (item is null) continue;
            yield return new IbgeMunicipalityRecord(
                item.Id,
                item.Nome,
                item.Uf?.Id ?? 0
            );
        }
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        var maxAttempts = Math.Max(1, options.Value.MaxRetryAttempts);
        var client = httpClientFactory.CreateClient("ibge");

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var releaser = await rateLimiter.AcquireAsync(cancellationToken);
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
                var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if ((int)response.StatusCode == 429)
                {
                    var retryAfter = response.Headers.RetryAfter?.Delta
                        ?? TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, attempt) + Random.Shared.NextDouble()));
                    logger.LogWarning("IBGE rate limit hit (429). Retrying after {RetryAfter}s.", retryAfter.TotalSeconds);
                    response.Dispose();
                    await Task.Delay(retryAfter, cancellationToken);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var statusCode = (int)response.StatusCode;
                    if (statusCode >= 500 && attempt < maxAttempts)
                    {
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt) + Random.Shared.NextDouble());
                        logger.LogWarning("IBGE server error ({StatusCode}). Retry attempt {Attempt} after {Delay}s.", statusCode, attempt, delay.TotalSeconds);
                        response.Dispose();
                        await Task.Delay(delay, cancellationToken);
                        continue;
                    }

                    response.EnsureSuccessStatusCode();
                }

                return response;
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt) + Random.Shared.NextDouble());
                logger.LogWarning(ex, "HTTP request failed. Retry attempt {Attempt} after {Delay}s.", attempt, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new HttpRequestException($"Failed to execute request against IBGE after {maxAttempts} attempts.");
    }
}

