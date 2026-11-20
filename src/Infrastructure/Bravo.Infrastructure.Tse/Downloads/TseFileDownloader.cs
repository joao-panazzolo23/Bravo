using System.Net;
using System.Security.Cryptography;
using Bravo.Application.Logging;
using Bravo.Infrastructure.Tse.Options;
using Bravo.Infrastructure.Tse.RateLimiting;
using Bravo.Infrastructure.Tse.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bravo.Infrastructure.Tse.Downloads;

public sealed class TseFileDownloader(
    IHttpClientFactory httpClientFactory,
    TseRateLimiter rateLimiter,
    TseBackoffPolicy backoff,
    IOptions<TseOptions> options,
    ILogger<TseFileDownloader> logger)
{
    private readonly TseOptions _options = options.Value;

    public async Task<DownloadedFile> DownloadAsync(
        string relativeUrl,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        ProcessingLog.DownloadStarted(logger, relativeUrl);

        Directory.CreateDirectory(_options.TempDirectory);

        var destination = Path.Combine(_options.TempDirectory, fileName);
        var partial = destination + ".part";

        try
        {
            var result = await DownloadWithRetryAsync(relativeUrl, partial, cancellationToken);

            File.Move(partial, destination, overwrite: true);

            ProcessingLog.DownloadCompleted(logger, relativeUrl, result.SizeBytes, result.Sha256);

            return new DownloadedFile(destination, fileName, result.SizeBytes, result.Sha256, relativeUrl);
        }
        finally
        {
            if (File.Exists(partial))
                File.Delete(partial);
        }
    }

    private async Task<(long SizeBytes, string Sha256)> DownloadWithRetryAsync(
        string relativeUrl,
        string partialPath,
        CancellationToken cancellationToken)
    {
        Exception? last = null;

        for (var attempt = 1; attempt <= backoff.MaxAttempts; attempt++)
        {
            try
            {
                return await DownloadOnceAsync(relativeUrl, partialPath, cancellationToken);
            }
            catch (TseTransientException exception)
            {
                last = exception;

                if (attempt == backoff.MaxAttempts)
                    break;

                var delay = backoff.GetDelay(attempt, exception.RetryAfter);

                ProcessingLog.DownloadRetrying(
                    logger,
                    relativeUrl,
                    delay.TotalMilliseconds,
                    attempt,
                    backoff.MaxAttempts,
                    exception);

                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new TseDownloadException(
            $"Failed to download '{relativeUrl}' after {backoff.MaxAttempts} attempt(s).",
            last);
    }

    private async Task<(long SizeBytes, string Sha256)> DownloadOnceAsync(
        string relativeUrl,
        string partialPath,
        CancellationToken cancellationToken)
    {
        using var lease = await rateLimiter.AcquireAsync(cancellationToken);

        if (!lease.IsAcquired)
            throw new TseDownloadException("The TSE download concurrency limit is exhausted.");

        var client = httpClientFactory.CreateClient(TseHttpClient.Name);

        using var response = await client.GetAsync(
            relativeUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new TseTransientException(
                "TSE rate limit was reached.",
                response.Headers.RetryAfter?.Delta);
        }

        if ((int)response.StatusCode >= 500)
            throw new TseTransientException($"TSE returned {(int)response.StatusCode}.");

        if (!response.IsSuccessStatusCode)
            throw new TseDownloadException($"TSE returned {(int)response.StatusCode} for '{relativeUrl}'.");

        var declaredLength = response.Content.Headers.ContentLength;

        if (declaredLength > _options.MaxDownloadBytes)
        {
            throw new TseDownloadException(
                $"Declared size {declaredLength} exceeds the configured limit of {_options.MaxDownloadBytes} bytes.");
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = new FileStream(
            partialPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            useAsync: true);

        using var hasher = SHA256.Create();
        var buffer = new byte[64 * 1024];
        long total = 0;

        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);

            if (read == 0)
                break;

            total += read;

            if (total > _options.MaxDownloadBytes)
                throw new TseDownloadException($"Download exceeded the limit of {_options.MaxDownloadBytes} bytes.");

            hasher.TransformBlock(buffer, 0, read, null, 0);
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        hasher.TransformFinalBlock([], 0, 0);

        var checksum = Convert.ToHexStringLower(hasher.Hash!);

        return (total, checksum);
    }
}
