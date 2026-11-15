using Bravo.Application.Importing.Exceptions;
using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Enums;
using Bravo.IntegrationTests.Configurations;
using FluentAssertions;

namespace Bravo.IntegrationTests.Importing;

[Collection("Database")]
public sealed class ConcurrencyIntegrationTests(PostgresContainerFixture pg) : ImportingTestBase(pg)
{
    [Fact]
    public async Task TwoWorkersCannotClaimTheSameWorkUnit()
    {
        var job = await CreateJobAsync();
        await MarkJobRunningAsync(job.Id);

        var firstWorker = await ClaimNextAsync(job.Id, "worker-a");
        var secondWorker = await ClaimNextAsync(job.Id, "worker-b");

        firstWorker.Should().NotBeNull();
        secondWorker.Should().BeNull();
        firstWorker!.LeaseOwner.Should().Be("worker-a");
    }

    [Fact]
    public async Task ExpiredLeaseCanBeReclaimedWithANewFencingToken()
    {
        var job = await CreateJobAsync();
        await MarkJobRunningAsync(job.Id);

        var firstWorker = await ClaimNextAsync(job.Id, "worker-a", leaseSeconds: 1);
        await Task.Delay(TimeSpan.FromMilliseconds(1_200));

        var secondWorker = await ClaimNextAsync(job.Id, "worker-b", leaseSeconds: 60);

        secondWorker.Should().NotBeNull();
        secondWorker!.Id.Should().Be(firstWorker!.Id);
        secondWorker.LeaseOwner.Should().Be("worker-b");
        secondWorker.FencingToken.Should().Be(firstWorker.FencingToken + 1);
    }

    [Fact]
    public async Task StaleWorkerCannotCommitAfterLosingOwnership()
    {
        var job = await CreateJobAsync();
        await MarkJobRunningAsync(job.Id);

        var staleWorker = await ClaimNextAsync(job.Id, "worker-a", leaseSeconds: 1);
        await Task.Delay(TimeSpan.FromMilliseconds(1_200));

        var newOwner = await ClaimNextAsync(job.Id, "worker-b", leaseSeconds: 60);
        newOwner.Should().NotBeNull();

        var action = async () => await ApplyBatchAsync(
            staleWorker!,
            [CreateState(131)],
            nextCursor: null,
            isFinalBatch: true);

        await action.Should().ThrowAsync<ImportLeaseLostException>();
        (await CountStatesAsync()).Should().Be(0);

        var workUnit = (await GetWorkUnitsAsync(job.Id)).Single();
        workUnit.Status.Should().Be(ImportWorkUnitStatus.Running);
        workUnit.LeaseOwner.Should().Be("worker-b");
        workUnit.Cursor.Should().BeNull();
    }

    [Fact]
    public async Task CompletedWorkUnitsAreNotClaimedAgain()
    {
        var job = await CreateJobAsync(unitKeys: ["first", "second"]);
        await MarkJobRunningAsync(job.Id);

        var first = await ClaimNextAsync(job.Id, "worker-a");
        first!.UnitKey.Should().Be("first");

        await ApplyBatchAsync(first, [], nextCursor: null, isFinalBatch: true);

        var second = await ClaimNextAsync(job.Id, "worker-a");
        second.Should().NotBeNull();
        second!.UnitKey.Should().Be("second");

        await ApplyBatchAsync(second, [], nextCursor: null, isFinalBatch: true);

        var jobAfterCompletion = await GetJobAsync(job.Id);
        jobAfterCompletion!.Status.Should().Be(ImportJobStatus.Completed);
        (await ClaimNextAsync(job.Id, "worker-b")).Should().BeNull();
    }

    [Fact]
    public async Task CancelledJobIsNotRecoveredOrExecuted()
    {
        Source.Add(CreateState(141));
        var job = await CreateJobAsync();

        (await CancelJobAsync(job.Id)).Should().BeTrue();

        (await ListRecoverableIdsAsync()).Should().NotContain(job.Id);

        await RunJobAsync(job.Id);

        var cancelledJob = await GetJobAsync(job.Id);
        var workUnit = (await GetWorkUnitsAsync(job.Id)).Single();

        cancelledJob!.Status.Should().Be(ImportJobStatus.Cancelled);
        workUnit.Status.Should().Be(ImportWorkUnitStatus.Cancelled);
        (await CountStatesAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CancellationPreventsInFlightBatchFromCommitting()
    {
        var job = await CreateJobAsync();
        await MarkJobRunningAsync(job.Id);

        var workUnit = await ClaimNextAsync(job.Id, "worker-a");
        workUnit.Should().NotBeNull();

        (await CancelJobAsync(job.Id)).Should().BeTrue();

        var action = async () => await ApplyBatchAsync(
            workUnit!,
            [CreateState(151)],
            nextCursor: null,
            isFinalBatch: true);

        await action.Should().ThrowAsync<ImportLeaseLostException>();
        (await CountStatesAsync()).Should().Be(0);

        var cancelledJob = await GetJobAsync(job.Id);
        cancelledJob!.Status.Should().Be(ImportJobStatus.Cancelled);
    }

    [Fact]
    public async Task OverlappingScopeCannotStartTwice()
    {
        var first = CreateOverlapJob("Overlap");
        (await TryCreateJobAsync(first, "states")).Should().BeTrue();

        var duplicate = CreateOverlapJob("Overlap");
        (await TryCreateJobAsync(duplicate, "states")).Should().BeFalse();

        var otherScope = CreateOverlapJob("Overlap");
        (await TryCreateJobAsync(otherScope, "municipalities")).Should().BeTrue();

        var otherType = CreateOverlapJob("Distinct");
        (await TryCreateJobAsync(otherType, "states")).Should().BeTrue();
    }

    private static ImportJob CreateOverlapJob(string importType)
        => ImportJob.Create(importType, ImportConflictStrategy.DoNothing, 10, 3, 60);
}
