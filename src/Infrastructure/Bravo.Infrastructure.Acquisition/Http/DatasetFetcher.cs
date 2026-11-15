using System.Security.Cryptography;
using Bravo.Application.Acquisition.Options;
using Bravo.Application.Acquisition.Providers;
using Bravo.Application.Acquisition.Services;
using Bravo.Infrastructure.Acquisition.RateLimiting;
using Microsoft.Extensions.Logging;
using Paramore.Fences;

namespace Bravo.Infrastructure.Acquisition.Http;

/// <summary>
/// Native HTTP downloader shared by all providers. It streams the response to a temporary
/// file, enforces the configured size limit, computes the SHA-256 of the received bytes and
/// supports conditional requests so an unchanged dataset is not downloaded again.
/// </summary>
public sealed class DatasetFetcher(
    HttpClient httpClient,
    ResiliencePipeline<HttpResponseMessage> pipeline,
    HostRateLimiterRegistry limiters,
    ITemporaryFileStore fileStore,
    ProviderOptions policy,
    AcquisitionOptions options,
    ILogger logger)
{
    private const int BufferSize = 81920;

    public async Task<CachedDownload?> DownloadAsync(
        Guid executionId,
        string datasetKey,
        Uri source,
        ConditionalRequest? conditional,
        CancellationToken ct)
    {
        ValidateHost(source, policy);

        // An interrupted execution may leave a fully downloaded and verified file behind.
        // Reusing it avoids downloading the same archive twice during a retry, while the size
        // and checksum check guarantees a partial file is never parsed.
        if (fileStore.FindVerified(executionId, datasetKey) is { } cached)
        {
            logger.LogInformation(
                "Reusing verified temporary download for {DatasetKey} ({Bytes} bytes).",
                datasetKey,
                cached.SizeBytes);
            return cached;
        }

        fileStore.EnsureRootExists();
        var extension = Path.GetExtension(source.AbsolutePath);
        if (string.IsNullOrWhiteSpace(extension) || extension.Length > 10)
        {
            extension = ".bin";
        }

        var targetPath = fileStore.CreatePath(executionId, datasetKey, extension);
        var limiter = limiters.For(source);

        using var lease = await limiter.AcquireAsync(ct);

        // A fresh request is created for every attempt: HttpRequestMessage cannot be sent
        // twice, and the retry pipeline re-invokes the delegate on transient failures.
        using var response = await pipeline.ExecuteAsync(
            token =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, source);
                request.Headers.TryAddWithoutValidation("User-Agent", policy.UserAgent);
                ApplyConditionalHeaders(request, conditional);
                return new ValueTask<HttpResponseMessage>(
                    httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token));
            },
            ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
        {
            logger.LogInformation("Dataset {DatasetKey} was not modified at the source.", datasetKey);
            return null;
        }

        response.EnsureSuccessStatusCode();

        var declaredLength = response.Content.Headers.ContentLength;
        if (declaredLength > options.MaxDownloadBytes)
        {
            throw new InvalidOperationException(
                $"Dataset {datasetKey} declares {declaredLength} bytes, above the configured limit of {options.MaxDownloadBytes}.");
        }

        long total = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        try
        {
            await using var sourceStream = await response.Content.ReadAsStreamAsync(ct);
            await using var destination = new FileStream(
                targetPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var buffer = new byte[BufferSize];
            int read;
            while ((read = await sourceStream.ReadAsync(buffer, ct)) > 0)
            {
                total += read;
                if (total > options.MaxDownloadBytes)
                {
                    throw new InvalidOperationException(
                        $"Dataset {datasetKey} exceeded the {options.MaxDownloadBytes} byte download limit.");
                }

                hash.AppendData(buffer, 0, read);
                await destination.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }
        catch
        {
            // A truncated archive must never survive a failed download: a later retry could
            // otherwise parse a partial dataset as if it were complete.
            fileStore.TryDelete(targetPath);
            throw;
        }

        if (total == 0)
        {
            fileStore.TryDelete(targetPath);
            throw new InvalidOperationException($"Dataset {datasetKey} returned an empty body.");
        }

        var sha = Convert.ToHexStringLower(hash.GetHashAndReset());

        logger.LogInformation(
            "Downloaded {DatasetKey} ({Bytes} bytes, sha256 {Hash}) from {Host}.",
            datasetKey,
            total,
            sha,
            source.Host);

        var result = new CachedDownload(
            targetPath,
            total,
            sha,
            DateTime.UtcNow,
            response.Headers.ETag?.Tag,
            response.Content.Headers.LastModified);

        fileStore.SaveMetadata(targetPath, result);
        return result;
    }

    /// <summary>
    /// Streams a small remote document (such as a provider catalog) into memory.
    /// </summary>
    public async Task<string> GetStringAsync(Uri source, long maxBytes, CancellationToken ct)
    {
        ValidateHost(source, policy);
        var limiter = limiters.For(source);

        using var lease = await limiter.AcquireAsync(ct);
        using var response = await pipeline.ExecuteAsync(
            token => new ValueTask<HttpResponseMessage>(
                httpClient.GetAsync(source, HttpCompletionOption.ResponseHeadersRead, token)),
            ct);

        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength > maxBytes)
        {
            throw new InvalidOperationException($"Document at {source} is larger than the {maxBytes} byte limit.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[BufferSize];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                throw new InvalidOperationException($"Document at {source} is larger than the {maxBytes} byte limit.");
            }

            buffer.Write(chunk, 0, read);
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    public static void ValidateHost(Uri source, ProviderOptions policy)
    {
        if (source.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"Refusing to fetch {source} over an insecure scheme.");
        }

        var allowed = policy.AllowedHosts.Any(h =>
            string.Equals(h, source.Host, StringComparison.OrdinalIgnoreCase));

        if (!allowed)
        {
            throw new InvalidOperationException($"Host '{source.Host}' is not allowed for this provider.");
        }
    }

    private static void ApplyConditionalHeaders(HttpRequestMessage request, ConditionalRequest? conditional)
    {
        if (conditional is null)
        {
            return;
        }

        if (conditional.ETag is { } etag)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        }

        if (conditional.LastModified is { } lastModified)
        {
            request.Headers.IfModifiedSince = lastModified;
        }
    }
}