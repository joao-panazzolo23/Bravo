using Bravo.Application.Imports.Common.Exceptions;
using Bravo.Application.Imports.Exceptions;
using Bravo.Domain.Importing.Enums;
using Bravo.Domain.Importing.Models;
using Bravo.Domain.Importing.Repositories;
using Bravo.IntegrationTests.Configurations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.IntegrationTests.Importing;

[Collection("Database")]
public sealed class RecoveryIntegrationTests(PostgresContainerFixture pg) : ImportingTestBase(pg)
{
    [Fact]
    public async Task AbandonedJobIsRecoveredAndRestartedFromTheBeginning()
    {
        Source.Operations = 5;
        Source.RecordsPerOperation = 2;

        var jobId = await CreateJobAsync();
        await ClaimAsync(jobId, "crashed-worker", leaseSeconds: 1);

        await Task.Delay(TimeSpan.FromMilliseconds(1_300));

        (await ListRecoverableIdsAsync()).Should().Contain(jobId);

        var completed = await RunJobAsync(jobId);

        completed!.Status.Should().Be(ImportJobStatus.Completed);
        completed.CompletedOperations.Should().Be(Source.Operations);
        completed.TotalProcessedRecords.Should().Be(Source.Operations * Source.RecordsPerOperation);
        completed.AttemptCount.Should().Be(2);
        Source.ExecutionCount.Should().Be(1);
    }

    [Fact]
    public async Task StartupRecoveryReleasesJobsOwnedByTheSameMachine()
    {
        var jobId = await CreateJobAsync();
        await ClaimAsync(jobId, $"{Environment.MachineName}:crashed");

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IImportJobRepository>();

        var released = await jobs.ReleaseOwnedByAsync($"{Environment.MachineName}:");

        released.Should().BeGreaterThan(0);

        var job = await GetJobAsync(jobId);

        job!.Status.Should().Be(ImportJobStatus.Pending);
        (await ListRecoverableIdsAsync()).Should().Contain(jobId);
    }

    [Fact]
    public async Task StartupRecoveryDoesNotReleaseJobsFromAnotherMachine()
    {
        var jobId = await CreateJobAsync();
        await ClaimAsync(jobId, "some-other-host:worker");

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IImportJobRepository>();

        await jobs.ReleaseOwnedByAsync($"{Environment.MachineName}:");

        (await GetJobAsync(jobId))!.Status.Should().Be(ImportJobStatus.Running);
    }

    [Fact]
    public async Task TransientFailureSchedulesAnotherAttempt()
    {
        Source.Failure = attempt => attempt == 1
            ? new ImportTransientException("Temporary outage.")
            : null;

        var jobId = await CreateJobAsync(maxAttempts: 3);

        var first = await RunJobAsync(jobId);

        first!.Status.Should().Be(ImportJobStatus.Pending);
        first.NextAttemptAt.Should().NotBeNull();
        first.LastError.Should().Contain("Temporary outage");

        // The worker only reclaims a scheduled retry once its backoff has elapsed.
        var delay = first.NextAttemptAt!.Value - DateTime.UtcNow;

        if (delay > TimeSpan.Zero)
            await Task.Delay(delay + TimeSpan.FromMilliseconds(100));

        var second = await RunJobAsync(jobId);

        second!.Status.Should().Be(ImportJobStatus.Completed);
        Source.ExecutionCount.Should().Be(2);
    }

    [Fact]
    public async Task PermanentFailureIsNotRetried()
    {
        Source.Failure = _ => new ImportPermanentException("Invalid source configuration.");

        var jobId = await CreateJobAsync(maxAttempts: 5);

        var job = await RunJobAsync(jobId);

        job!.Status.Should().Be(ImportJobStatus.Failed);
        job.NextAttemptAt.Should().BeNull();
        (await ListRecoverableIdsAsync()).Should().NotContain(jobId);
    }

    [Fact]
    public async Task TransientFailureStopsAtMaximumAttempts()
    {
        Source.Failure = _ => new ImportTransientException("Persistent outage.");

        var jobId = await CreateJobAsync(maxAttempts: 1);

        var job = await RunJobAsync(jobId);

        job!.Status.Should().Be(ImportJobStatus.Failed);
        job.NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public async Task ProgressWrittenByAConcurrentSourceIsCompleteAndMonotonic()
    {
        Source.Operations = 6;
        Source.RecordsPerOperation = 3;
        Source.OperationDelayMilliseconds = 20;

        var jobId = await CreateJobAsync(maxParallelism: 3);

        var job = await RunJobAsync(jobId);

        job!.CompletedOperations.Should().Be(6);
        job.TotalProcessedRecords.Should().Be(18);
        job.ProgressUpdatedAt.Should().NotBeNull();
    }
}
