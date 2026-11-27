using Bravo.Application.Imports.Common.Execution;

using Bravo.Application.Imports.Execution;
using Bravo.Domain.Importing.Enums;
using Bravo.IntegrationTests.Configurations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.IntegrationTests.Importing;

/// <summary>
/// Covers the job lifecycle that used to live inside <c>ImportWorker</c> and now belongs to the
/// application-layer executor. These run the production executor against PostgreSQL, so a
/// regression in claim/lease/settle behaviour is caught here rather than in production.
/// </summary>
[Collection("Database")]
public sealed class ImportJobExecutorTests(PostgresContainerFixture pg) : ImportingTestBase(pg)
{
    [Fact]
    public async Task ExecutesClaimedJobAndCompletesIt()
    {
        Source.Operations = 3;
        Source.RecordsPerOperation = 5;

        var jobId = await CreateJobAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<ImportJobExecutor>();

        var result = await executor.ExecuteAsync(jobId, "worker-x", CancellationToken.None);

        result.Outcome.Should().Be(ImportJobExecutionOutcome.Completed);

        var job = await GetJobAsync(jobId);

        job!.Status.Should().Be(ImportJobStatus.Completed);
        job.CompletedOperations.Should().Be(3);
        job.TotalProcessedRecords.Should().Be(15);
        job.LeaseOwner.Should().BeNull();
        Source.ExecutionCount.Should().Be(1);
    }

    [Fact]
    public async Task ReportsNotClaimedWhenAnotherWorkerOwnsTheJob()
    {
        var jobId = await CreateJobAsync();
        await ClaimAsync(jobId, "other-worker");

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<ImportJobExecutor>();

        var result = await executor.ExecuteAsync(jobId, "worker-x", CancellationToken.None);

        result.Outcome.Should().Be(ImportJobExecutionOutcome.NotClaimed);
        Source.ExecutionCount.Should().Be(0);
    }

    [Fact]
    public async Task FailedJobIsScheduledForRetryWhenTransient()
    {
        Source.Failure = _ => new Bravo.Application.Imports.Exceptions.ImportTransientException(
            "temporary failure", TimeSpan.FromMilliseconds(10));

        var jobId = await CreateJobAsync(maxAttempts: 3);

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<ImportJobExecutor>();

        var result = await executor.ExecuteAsync(jobId, "worker-x", CancellationToken.None);

        result.Outcome.Should().Be(ImportJobExecutionOutcome.Completed);

        var job = await GetJobAsync(jobId);

        // A retryable failure returns the job to the queue, not to a terminal Failed state.
        job!.Status.Should().Be(ImportJobStatus.Pending);
        job.NextAttemptAt.Should().NotBeNull();
        job.LeaseOwner.Should().BeNull();
        job.LastError.Should().Contain("temporary failure");
    }

    [Fact]
    public async Task PermanentFailureIsNotScheduledForRetry()
    {
        Source.Failure = _ => new Bravo.Application.Imports.Common.Exceptions.ImportPermanentException(
            "permanent failure");

        var jobId = await CreateJobAsync(maxAttempts: 3);

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<ImportJobExecutor>();

        await executor.ExecuteAsync(jobId, "worker-x", CancellationToken.None);

        var job = await GetJobAsync(jobId);

        job!.Status.Should().Be(ImportJobStatus.Failed);
        job.NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public async Task ShutdownCancellationReleasesTheJobForAnotherWorker()
    {
        Source.Operations = 50;
        Source.OperationDelayMilliseconds = 100;

        var jobId = await CreateJobAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<ImportJobExecutor>();

        using var stopping = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var result = await executor.ExecuteAsync(jobId, "worker-x", stopping.Token);

        result.Outcome.Should().Be(ImportJobExecutionOutcome.ReleasedForShutdown);

        var job = await GetJobAsync(jobId);

        job!.Status.Should().Be(ImportJobStatus.Pending);
        job.LeaseOwner.Should().BeNull();
    }

    [Fact]
    public async Task TwoExecutionsDoNotShareScopedState()
    {
        Source.Operations = 2;
        Source.RecordsPerOperation = 1;

        var first = await CreateJobAsync(scope: "FAKE:one");
        var second = await CreateJobAsync(scope: "FAKE:two");

        await using var firstScope = Pg.Factory.Services.CreateAsyncScope();
        await using var secondScope = Pg.Factory.Services.CreateAsyncScope();

        var executorA = firstScope.ServiceProvider.GetRequiredService<ImportJobExecutor>();
        var executorB = secondScope.ServiceProvider.GetRequiredService<ImportJobExecutor>();

        var results = await Task.WhenAll(
            executorA.ExecuteAsync(first, "worker-a", CancellationToken.None),
            executorB.ExecuteAsync(second, "worker-b", CancellationToken.None));

        results.Should().OnlyContain(result => result.Outcome == ImportJobExecutionOutcome.Completed);

        (await GetJobAsync(first))!.Status.Should().Be(ImportJobStatus.Completed);
        (await GetJobAsync(second))!.Status.Should().Be(ImportJobStatus.Completed);

        Source.ExecutionCount.Should().Be(2);
    }

    [Fact]
    public async Task RecoveryServiceReleasesJobsOwnedByTheSameMachine()
    {
        var jobId = await CreateJobAsync();
        await ClaimAsync(jobId, $"{Environment.MachineName}:crashed");

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var recovery = scope.ServiceProvider.GetRequiredService<ImportJobRecoveryService>();

        var released = await recovery.RecoverAbandonedAsync(Environment.MachineName);

        released.Should().BeGreaterThan(0);

        var job = await GetJobAsync(jobId);

        job!.Status.Should().Be(ImportJobStatus.Pending);
        job.LeaseOwner.Should().BeNull();
    }
}
