using Bravo.Application.Importing.Options;
using Bravo.Application.Importing.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bravo.UnitTests.Importing;

/// <summary>
/// The governor is the only thing standing between "each scope honours its own limit" and a
/// bounded process-wide peak, so its limits and permit accounting are tested directly.
/// </summary>
public sealed class ImportResourceGovernorTests
{
    private static ImportResourceGovernor Build(ImportResourceOptions options)
        => new(Microsoft.Extensions.Options.Options.Create(options), NullLogger<ImportResourceGovernor>.Instance);

    [Fact]
    public async Task ResultScopeLimitIsSharedAcrossCallers()
    {
        using var governor = Build(new ImportResourceOptions { MaxConcurrentResultScopes = 2 });

        var first = await governor.AcquireResultScopeAsync();
        var second = await governor.AcquireResultScopeAsync();

        var third = governor.AcquireResultScopeAsync(CancellationToken.None);

        // Two held, so the third must wait rather than exceed the process-wide bound.
        await Task.Delay(100);
        third.IsCompleted.Should().BeFalse();

        first.Dispose();

        var acquired = await third;
        acquired.Dispose();

        second.Dispose();
    }

    [Fact]
    public async Task IndependentBudgetsDoNotBlockEachOther()
    {
        using var governor = Build(new ImportResourceOptions
        {
            MaxConcurrentResultScopes = 1,
            MaxConcurrentCsvReaders = 1,
            MaxConcurrentPersistenceOperations = 1,
            MaxConcurrentExtractions = 1
        });

        var scope = await governor.AcquireResultScopeAsync();
        var reader = await governor.AcquireCsvReaderAsync();
        var persistence = await governor.AcquirePersistenceAsync();
        var extraction = await governor.AcquireExtractionAsync();

        // Holding every other permit must not prevent a different resource from being taken,
        // which is what keeps a slow database from blocking extraction.
        scope.Dispose();
        reader.Dispose();
        persistence.Dispose();
        extraction.Dispose();

        await Task.CompletedTask;
    }

    [Fact]
    public async Task PermitIsReleasedWhenTheGuardedWorkThrows()
    {
        using var governor = Build(new ImportResourceOptions { MaxConcurrentResultScopes = 1 });

        await FluentActions.Awaiting(async () =>
        {
            using var permit = await governor.AcquireResultScopeAsync();
            throw new InvalidOperationException("work failed");
        }).Should().ThrowAsync<InvalidOperationException>();

        // The permit must be back, otherwise one failure would permanently shrink capacity.
        var acquired = await governor.AcquireResultScopeAsync().WaitAsync(TimeSpan.FromSeconds(2));

        acquired.Dispose();
    }

    [Fact]
    public async Task CancellationWhileWaitingDoesNotConsumeAPermit()
    {
        using var governor = Build(new ImportResourceOptions { MaxConcurrentResultScopes = 1 });

        var held = await governor.AcquireResultScopeAsync();

        using var cts = new CancellationTokenSource();
        var waiting = governor.AcquireResultScopeAsync(cts.Token);

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);

        held.Dispose();

        // Capacity must be intact after a cancelled wait.
        var acquired = await governor.AcquireResultScopeAsync().WaitAsync(TimeSpan.FromSeconds(2));

        acquired.Dispose();
    }

    [Fact]
    public async Task DoubleDisposeDoesNotInflateCapacity()
    {
        using var governor = Build(new ImportResourceOptions { MaxConcurrentResultScopes = 1 });

        var permit = await governor.AcquireResultScopeAsync();

        permit.Dispose();
        permit.Dispose();

        // A second release must be a no-op; otherwise the semaphore would allow two scopes.
        var first = await governor.AcquireResultScopeAsync().WaitAsync(TimeSpan.FromSeconds(2));
        var second = governor.AcquireResultScopeAsync();

        await Task.Delay(100);
        second.IsCompleted.Should().BeFalse();

        first.Dispose();
    }

    [Fact]
    public void ExposesConfiguredBatchDepth()
    {
        using var governor = Build(new ImportResourceOptions { MaxInFlightBatchesPerScope = 3 });

        governor.MaxInFlightBatchesPerScope.Should().Be(3);
    }
}
