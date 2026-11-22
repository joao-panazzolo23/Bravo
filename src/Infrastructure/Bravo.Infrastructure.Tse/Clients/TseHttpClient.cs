using System.Net;
using System.Security.Cryptography;
using Bravo.Application.Logging;
using Bravo.Infrastructure.Tse.Downloads;
using Bravo.Infrastructure.Tse.Options;
using Bravo.Infrastructure.Tse.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bravo.Infrastructure.Tse.Clients;

/// <summary>
/// todo: refactor this trash
/// </remarks>
public sealed class TseHttpClient(
    HttpClient client,
    // TseRateLimiter rateLimiter,
    TseBackoffPolicy backoff,
    IOptions<TseOptions> options,
    ILogger<TseHttpClient> logger)
{
    private const string CandidateArchivePath = "estatistica/sead/odsele/consulta_cand/consulta_cand_{0}.zip";
    private const string ResultsArchivePath = "estatistica/sead/odsele/votacao_secao/votacao_secao_{0}_{1}.zip";

    private readonly TseOptions _options = options.Value;

    public Task<DownloadedFile> DownloadCandidateArchiveAsync(
        int year,
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        var fileName = $"consulta_cand_{year}.zip";

        return DownloadAsync(string.Format(CandidateArchivePath, year), directoryPath, fileName, cancellationToken);
    }

    public Task<DownloadedFile> DownloadSectionResultsArchiveAsync(
        int year,
        string stateAcronym,
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateAcronym);

        var state = stateAcronym.ToUpperInvariant();
        var fileName = $"votacao_secao_{year}_{state}.zip";

        return DownloadAsync(
            string.Format(ResultsArchivePath, year, state),
            directoryPath,
            fileName,
            cancellationToken);
    }

    private async Task<DownloadedFile> DownloadAsync(
        string relativeUrl,
        string directoryPath,
        string fileName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        ProcessingLog.DownloadStarted(logger, relativeUrl);

        Directory.CreateDirectory(directoryPath);

        var destination = Path.Combine(directoryPath, fileName);
        var partial = destination + ".part";

        try
        {
            var (sizeBytes, sha256) = await DownloadWithRetryAsync(relativeUrl, partial, cancellationToken);

            File.Move(partial, destination, overwrite: true);

            ProcessingLog.DownloadCompleted(logger, relativeUrl, sizeBytes, sha256);

            return new DownloadedFile(destination, fileName, sizeBytes, sha256, relativeUrl);
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

        using var response = await client.GetAsync(
            relativeUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        EnsureSuccess(response, relativeUrl);
        EnsureDeclaredSizeIsAllowed(response);

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = OpenPartialFile(partialPath);

        return await CopyWithChecksumAsync(source, target, cancellationToken);
    }

    /// <summary>
    /// Rejects a response whose declared length already exceeds the configured cap, so an
    /// oversized download is refused before any bytes are written.
    /// </summary>
    private void EnsureDeclaredSizeIsAllowed(HttpResponseMessage response)
    {
        var declaredLength = response.Content.Headers.ContentLength;

        if (declaredLength > _options.MaxDownloadBytes)
        {
            throw new TseDownloadException(
                $"Declared size {declaredLength} exceeds the configured limit of {_options.MaxDownloadBytes} bytes.");
        }
    }

    private static FileStream OpenPartialFile(string partialPath)
        => new(
            partialPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            useAsync: true);

    /// <summary>
    /// Streams the payload to disk while hashing it, enforcing the byte cap as it goes. The
    /// declared length can be absent or wrong, so the running total is the authoritative check.
    /// </summary>
    private async Task<(long SizeBytes, string Sha256)> CopyWithChecksumAsync(
        Stream source,
        Stream target,
        CancellationToken cancellationToken)
    {
        using var hasher = SHA256.Create();
        var buffer = new byte[64 * 1024];
        long total = 0;

        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);

            if (read == 0)
                break;

            // The cap is enforced against the running total rather than the declared length,
            // because a chunked response has no Content-Length and a declared value can lie.
            total = EnsureWithinLimit(total + read);

            hasher.TransformBlock(buffer, 0, read, null, 0);
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        hasher.TransformFinalBlock([], 0, 0);

        return (total, Convert.ToHexStringLower(hasher.Hash!));
    }

    /// <summary>
    /// Rejects the download as soon as the bytes already received exceed the configured cap, so
    /// the partial file is abandoned before the remaining payload is transferred.
    /// </summary>
    private long EnsureWithinLimit(long totalBytes)
    {
        if (totalBytes > _options.MaxDownloadBytes)
        {
            throw new TseDownloadException(
                $"Download exceeded the limit of {_options.MaxDownloadBytes} bytes.");
        }

        return totalBytes;
    }

    /// <summary>
    /// Classifies a response so the caller can distinguish a retryable provider failure from a
    /// permanent one. 429 and 5xx are transient; any other non-success is permanent.
    /// </summary>
    private static void EnsureSuccess(HttpResponseMessage response, string relativeUrl)
    {
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
    }
}
