using Bravo.Application.Importing.Exceptions;
using Bravo.Domain.Importing.Enums;
using Bravo.IntegrationTests.Configurations;
using FluentAssertions;

namespace Bravo.IntegrationTests.Importing;

[Collection("Database")]
public sealed class CheckpointIntegrationTests(PostgresContainerFixture pg) : ImportingTestBase(pg)
{
    [Fact]
    public async Task SuccessfulImportAdvancesCheckpointAndCompletesJob()
    {
        Source.Add(
            CreateState(11),
            CreateState(12),
            CreateState(13),
            CreateState(14),
            CreateState(15));

        var job = await CreateJobAsync(batchSize: 2);

        await RunJobAsync(job.Id);

        var completedJob = await GetJobAsync(job.Id);
        var workUnit = (await GetWorkUnitsAsync(job.Id)).Single();

        completedJob!.Status.Should().Be(ImportJobStatus.Completed);
        completedJob.TotalProcessedRecords.Should().Be(5);
        completedJob.TotalFailedRecords.Should().Be(0);
        workUnit.Status.Should().Be(ImportWorkUnitStatus.Completed);
        workUnit.Cursor.Should().BeNull();
        workUnit.ProcessedRecords.Should().Be(5);
        (await CountStatesAsync()).Should().Be(5);
    }

    [Fact]
    public async Task FailedBatchDoesNotAdvanceCheckpoint()
    {
        Source.Add(
            CreateState(21),
            CreateState(22),
            CreateState(23),
            CreateState(24));
        Source.FailAt(2, new ImportPermanentException("Permanent source failure."));

        var job = await CreateJobAsync(batchSize: 2);

        await RunJobAsync(job.Id);

        var failedJob = await GetJobAsync(job.Id);
        var workUnit = (await GetWorkUnitsAsync(job.Id)).Single();

        failedJob!.Status.Should().Be(ImportJobStatus.Failed);
        workUnit.Status.Should().Be(ImportWorkUnitStatus.Failed);
        workUnit.Cursor.Should().Be("2");
        workUnit.ProcessedRecords.Should().Be(2);
        workUnit.AttemptCount.Should().Be(1);
        (await CountStatesAsync()).Should().Be(2);
    }

    [Fact]
    public async Task DatabaseFailureRollsBackBatchAndPreservesCheckpoint()
    {
        Source.Add(
            CreateState(31),
            CreateState(32),
            CreateState(33, abbreviation: "TOOLONG"));

        var job = await CreateJobAsync(batchSize: 3);

        await RunJobAsync(job.Id);

        var failedJob = await GetJobAsync(job.Id);
        var workUnit = (await GetWorkUnitsAsync(job.Id)).Single();

        failedJob!.Status.Should().Be(ImportJobStatus.Failed);
        workUnit.Status.Should().Be(ImportWorkUnitStatus.Failed);
        workUnit.Cursor.Should().BeNull();
        workUnit.ProcessedRecords.Should().Be(0);
        (await CountStatesAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RestartResumesFromLastCommittedCheckpoint()
    {
        Source.Add(
            CreateState(41),
            CreateState(42),
            CreateState(43),
            CreateState(44),
            CreateState(45));

        var job = await CreateJobAsync(batchSize: 2, leaseDurationSeconds: 1);
        await MarkJobRunningAsync(job.Id);

        var workUnit = await ClaimNextAsync(job.Id, "crashed-worker", leaseSeconds: 1);
        workUnit.Should().NotBeNull();

        await ApplyBatchAsync(
            workUnit!,
            [CreateState(41), CreateState(42)],
            nextCursor: "2",
            isFinalBatch: false,
            leaseSeconds: 1);

        await Task.Delay(TimeSpan.FromMilliseconds(1_200));

        await RunJobAsync(job.Id, "recovery-worker");

        var completedJob = await GetJobAsync(job.Id);
        var recoveredUnit = (await GetWorkUnitsAsync(job.Id)).Single();

        completedJob!.Status.Should().Be(ImportJobStatus.Completed);
        completedJob.TotalProcessedRecords.Should().Be(5);
        recoveredUnit.Status.Should().Be(ImportWorkUnitStatus.Completed);
        recoveredUnit.ProcessedRecords.Should().Be(5);
        recoveredUnit.FencingToken.Should().Be(2);
        (await CountStatesAsync()).Should().Be(5);
    }
}
