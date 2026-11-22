using System.Diagnostics;
using Bravo.Application.Imports.Common.Options;
using Bravo.Application.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bravo.Application.Imports.Common.Execution;

/// <summary>
/// Process-wide gate for the memory-heavy stages of an import.
/// </summary>
/// <remarks>
/// Register this as a singleton. Every permit is a <see cref="SemaphoreSlim"/> slot shared by all
/// jobs in this process, so the real peak is bounded by configuration rather than by
/// (jobs x scopes x stages). Waiting is asynchronous and honours cancellation; a permit is
/// always released, including when the guarded work throws.
///
/// The governor deliberately does not model bytes. It bounds how many large files are open and
/// how many batches are queued, which is what actually determines whether memory stays bounded.
/// </remarks>
public sealed class ImportResourceGovernor : IDisposable
{
    private readonly SemaphoreSlim _resultScopes;
    private readonly SemaphoreSlim _candidateScopes;
    private readonly SemaphoreSlim _extractions;
    private readonly SemaphoreSlim _csvReaders;
    private readonly SemaphoreSlim _persistence;
    private readonly ImportResourceOptions _options;
    private readonly ILogger<ImportResourceGovernor> _logger;

    public ImportResourceGovernor(
        IOptions<ImportResourceOptions> options,
        ILogger<ImportResourceGovernor> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;

        _resultScopes = new SemaphoreSlim(_options.MaxConcurrentResultScopes);
        _candidateScopes = new SemaphoreSlim(_options.MaxConcurrentCandidateScopes);
        _extractions = new SemaphoreSlim(_options.MaxConcurrentExtractions);
        _csvReaders = new SemaphoreSlim(_options.MaxConcurrentCsvReaders);
        _persistence = new SemaphoreSlim(_options.MaxConcurrentPersistenceOperations);
    }

    /// <summary>Batches one scope may queue before its parser must wait.</summary>
    public int MaxInFlightBatchesPerScope => _options.MaxInFlightBatchesPerScope;

    public Task<IDisposable> AcquireResultScopeAsync(CancellationToken cancellationToken = default)
        => AcquireAsync(_resultScopes, "result scope", cancellationToken);

    public Task<IDisposable> AcquireCandidateScopeAsync(CancellationToken cancellationToken = default)
        => AcquireAsync(_candidateScopes, "candidate scope", cancellationToken);

    public Task<IDisposable> AcquireExtractionAsync(CancellationToken cancellationToken = default)
        => AcquireAsync(_extractions, "extraction", cancellationToken);

    public Task<IDisposable> AcquireCsvReaderAsync(CancellationToken cancellationToken = default)
        => AcquireAsync(_csvReaders, "csv reader", cancellationToken);

    public Task<IDisposable> AcquirePersistenceAsync(CancellationToken cancellationToken = default)
        => AcquireAsync(_persistence, "persistence", cancellationToken);

    /// <summary>
    /// Takes one permit, recording how long the caller had to wait so contention is visible
    /// instead of silently slowing the import down.
    /// </summary>
    private async Task<IDisposable> AcquireAsync(
        SemaphoreSlim gate,
        string resource,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        var waited = Stopwatch.GetElapsedTime(started);

        // Only log when the wait is actually significant, so a healthy run stays quiet.
        if (waited > TimeSpan.FromSeconds(1))
            ResourceLog.ResourcePermitWaited(_logger, resource, waited.TotalMilliseconds);

        return new Permit(gate);
    }

    public void Dispose()
    {
        _resultScopes.Dispose();
        _candidateScopes.Dispose();
        _extractions.Dispose();
        _csvReaders.Dispose();
        _persistence.Dispose();
    }

    /// <summary>
    /// Releases exactly once. A double dispose is treated as a programming error rather than
    /// silently over-releasing the semaphore and widening the real concurrency bound.
    /// </summary>
    private sealed class Permit(SemaphoreSlim gate) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 1)
                return;

            gate.Release();
        }
    }
}
