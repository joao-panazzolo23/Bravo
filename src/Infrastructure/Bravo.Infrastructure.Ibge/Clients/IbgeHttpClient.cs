using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Bravo.Application.Importing.Exceptions;
using Bravo.Infrastructure.Ibge.Models;
using Bravo.Infrastructure.Ibge.RateLimiting;

namespace Bravo.Infrastructure.Ibge.Clients;

/// <summary>
/// Encapsulates HTTP communication with the IBGE localities API.
/// </summary>
/// <remarks>
/// Both collections arrive as a single JSON document, so responses are decoded with
/// <see cref="JsonSerializer.DeserializeAsyncEnumerable{TValue}"/> and never materialised in
/// full. This type owns the endpoint, the rate limit and the status handling; validation and
/// mapping to domain types stay with the reader.
///
/// Usage: <c>await foreach (var dto in client.StreamStatesAsync(token)) { ... }</c>
/// </remarks>
public sealed class IbgeHttpClient(
    HttpClient client,
    IbgeRateLimiter rateLimiter)
{
    private const string StatesRequestUri = "api/v1/localidades/estados?orderBy=nome";
    private const string MunicipalitiesRequestUri = "api/v1/localidades/municipios?orderBy=nome";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IAsyncEnumerable<IbgeEstadoDto?> StreamStatesAsync(CancellationToken cancellationToken)
        => StreamAsync<IbgeEstadoDto>(StatesRequestUri, cancellationToken);

    public IAsyncEnumerable<IbgeMunicipalityDto?> StreamMunicipalitiesAsync(CancellationToken cancellationToken)
        => StreamAsync<IbgeMunicipalityDto>(MunicipalitiesRequestUri, cancellationToken);

    private async IAsyncEnumerable<TDto?> StreamAsync<TDto>(
        string requestUri,
        [EnumeratorCancellation] CancellationToken cancellationToken)
        where TDto : class
    {
        using var lease = await rateLimiter.AcquireAsync(cancellationToken);

        if (!lease.IsAcquired)
            throw new ImportTransientException("The IBGE request rate limit is exhausted.");

        using var response = await client.GetAsync(
            requestUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        EnsureSuccess(response);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<TDto>(
                           stream, JsonOptions, cancellationToken))
        {
            yield return item;
        }
    }

    /// <summary>
    /// Maps provider responses onto the import failure model: a throttled or server-side
    /// failure is worth retrying, any other non-success is permanent.
    /// </summary>
    private static void EnsureSuccess(HttpResponseMessage response)
    {
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
    }
}
