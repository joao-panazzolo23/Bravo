using Bravo.Domain.Importing.Enums;
using Bravo.Domain.Importing.Models;
using Bravo.Domain.Importing.Repositories;
using Bravo.IntegrationTests.Configurations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.IntegrationTests.Importing;

[Collection("Database")]
public sealed class JobLifecycleIntegrationTests(PostgresContainerFixture pg) : ImportingTestBase(pg)
{
    [Fact]
    public async Task RunningAJobCompletesItAndReportsProgress()
    {
        Source.Operations = 4;
        Source.RecordsPerOperation = 7;

        var jobId = await CreateJobAsync();

        var job = await RunJobAsync(jobId);

        job!.Status.Should().Be(ImportJobStatus.Completed);
        job.CompletedOperations.Should().Be(4);
        job.TotalProcessedRecords.Should().Be(28);
        job.CurrentOperation.Should().BeNull();
        job.LeaseOwner.Should().BeNull();
        job.CompletedAt.Should().NotBeNull();
        Source.ExecutionCount.Should().Be(1);
    }

    [Fact]
    public async Task ClaimingAPendingJobResetsProgressFromThePreviousRun()
    {
        var jobId = await CreateJobAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IImportJobRepository>();

        var claimed = await jobs.ClaimAsync(jobId, "worker-a", TimeSpan.FromSeconds(60));
        claimed.Should().NotBeNull();

        await jobs.ReportProgressAsync(
            jobId,
            "worker-a",
            new ImportJobProgress(ImportStage.Persisting, 10, 7, 0, 700, 0, "Operation 7"));

        await jobs.ReleaseAsync(jobId, "worker-a", "shutdown");

        var pending = await GetJobAsync(jobId);
        pending!.Status.Should().Be(ImportJobStatus.Pending);
        pending.CompletedOperations.Should().Be(7);

        var reclaimed = await ClaimAsync(jobId, "worker-b");

        reclaimed.Should().NotBeNull();
        reclaimed!.CompletedOperations.Should().Be(0);
        reclaimed.TotalProcessedRecords.Should().Be(0);
        reclaimed.Stage.Should().Be(ImportStage.Initializing);
        reclaimed.AttemptCount.Should().Be(2);
    }

    [Fact]
    public async Task CompletedJobIsNeverClaimedAgain()
    {
        var jobId = await CreateJobAsync();
        await RunJobAsync(jobId);

        (await ClaimAsync(jobId, "late-worker")).Should().BeNull();
    }

    [Fact]
    public async Task CancelledJobIsTerminalAndNotRecoverable()
    {
        var jobId = await CreateJobAsync();

        (await CancelJobAsync(jobId)).Should().BeTrue();
        (await CancelJobAsync(jobId)).Should().BeFalse();

        (await ListRecoverableIdsAsync()).Should().NotContain(jobId);

        var job = await GetJobAsync(jobId);
        job!.Status.Should().Be(ImportJobStatus.Cancelled);
        Source.ExecutionCount.Should().Be(0);
    }

    [Fact]
    public async Task CancelledJobCannotBeCompletedByALateWorker()
    {
        var jobId = await CreateJobAsync();
        await ClaimAsync(jobId, "worker-a");

        (await CancelJobAsync(jobId)).Should().BeTrue();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IImportJobRepository>();

        await jobs.CompleteAsync(
            jobId, "worker-a", ImportJobStatus.Completed, error: null);

        var job = await GetJobAsync(jobId);
        job!.Status.Should().Be(ImportJobStatus.Cancelled);
    }

    [Fact]
    public async Task GracefulShutdownReturnsJobToPending()
    {
        var jobId = await CreateJobAsync();
        await ClaimAsync(jobId, "worker-a");

        await ReleaseAsync(jobId, "worker-a");

        var job = await GetJobAsync(jobId);

        job!.Status.Should().Be(ImportJobStatus.Pending);
        job.LeaseOwner.Should().BeNull();
        (await ListRecoverableIdsAsync()).Should().Contain(jobId);
    }

    [Fact]
    public async Task FailedJobCanBeRequeuedAndExecutedAgain()
    {
        Source.Failure = _ => new Bravo.Application.Imports.Common.Exceptions.ImportPermanentException("Permanent source failure.");

        var jobId = await CreateJobAsync();

        var failed = await RunJobAsync(jobId);
        failed!.Status.Should().Be(ImportJobStatus.Failed);
        failed.LastError.Should().Contain("Permanent source failure");

        Source.Failure = null;

        var retry = await RetryJobAsync(jobId);
        retry.StatusCode.Should().Be(201);

        var completed = await RunJobAsync(jobId);

        completed!.Status.Should().Be(ImportJobStatus.Completed);
        completed.LastError.Should().BeNull();

        // The failed run and the successful retry are two executions of the same job.
        Source.ExecutionCount.Should().Be(2);
    }
}
