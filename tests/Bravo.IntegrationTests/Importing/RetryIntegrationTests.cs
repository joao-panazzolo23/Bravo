using Bravo.Application.Importing.Exceptions;
using Bravo.Domain.Importing.Enums;
using Bravo.IntegrationTests.Configurations;
using FluentAssertions;

namespace Bravo.IntegrationTests.Importing;

[Collection("Database")]
public sealed class RetryIntegrationTests(PostgresContainerFixture pg) : ImportingTestBase(pg)
{
    [Fact]
    public async Task TransientFailureRetriesFromLastCommittedCheckpoint()
    {
        Source.Add(
            CreateState(101),
            CreateState(102),
            CreateState(103),
            CreateState(104));
        Source.FailAt(2, new ImportTransientException("Temporary source outage."), occurrences: 1);

        var job = await CreateJobAsync(batchSize: 2, maxAttempts: 3);

        await RunJobAsync(job.Id);

        var completedJob = await GetJobAsync(job.Id);
        var workUnit = (await GetWorkUnitsAsync(job.Id)).Single();

        completedJob!.Status.Should().Be(ImportJobStatus.Completed);
        completedJob.TotalProcessedRecords.Should().Be(4);
        workUnit.Status.Should().Be(ImportWorkUnitStatus.Completed);
        workUnit.AttemptCount.Should().Be(1);
        workUnit.ProcessedRecords.Should().Be(4);
        (await CountStatesAsync()).Should().Be(4);
    }

    [Fact]
    public async Task TransientFailureStopsAtMaximumAttempts()
    {
        Source.Add(
            CreateState(111),
            CreateState(112),
            CreateState(113),
            CreateState(114));
        Source.FailAt(2, new ImportTransientException("Persistent temporary outage."), occurrences: 10);

        var job = await CreateJobAsync(batchSize: 2, maxAttempts: 2);

        await RunJobAsync(job.Id);

        var failedJob = await GetJobAsync(job.Id);
        var workUnit = (await GetWorkUnitsAsync(job.Id)).Single();

        failedJob!.Status.Should().Be(ImportJobStatus.Failed);
        workUnit.Status.Should().Be(ImportWorkUnitStatus.Failed);
        workUnit.AttemptCount.Should().Be(2);
        workUnit.Cursor.Should().Be("2");
        (await CountStatesAsync()).Should().Be(2);
    }

    [Fact]
    public async Task PermanentFailureIsNotRetried()
    {
        Source.Add(CreateState(121));
        Source.FailAt(0, new ImportPermanentException("Invalid source configuration."));

        var job = await CreateJobAsync(batchSize: 2, maxAttempts: 5);

        await RunJobAsync(job.Id);

        var workUnit = (await GetWorkUnitsAsync(job.Id)).Single();

        workUnit.Status.Should().Be(ImportWorkUnitStatus.Failed);
        workUnit.AttemptCount.Should().Be(1);
        (await CountStatesAsync()).Should().Be(0);
    }

    [Fact]
    public async Task FailedWorkUnitsCanBeRetriedAsANewJob()
    {
        Source.Add(CreateState(161));
        Source.FailAt(0, new ImportPermanentException("Invalid source configuration."));

        var job = await CreateJobAsync();

        (await RetryJobAsync(job.Id)).StatusCode.Should().Be(400);

        await RunJobAsync(job.Id);

        (await GetJobAsync(job.Id))!.Status.Should().Be(ImportJobStatus.Failed);

        Source.Reset();
        Source.Add(CreateState(161, name: "Retried"));

        var retry = await RetryJobAsync(job.Id);

        retry.StatusCode.Should().Be(201);
        retry.Data.Should().NotBeNull();

        await RunJobAsync(retry.Data!.Id);

        (await GetJobAsync(retry.Data.Id))!.Status.Should().Be(ImportJobStatus.Completed);
        (await GetStateNameAsync(161)).Should().Be("Retried");
    }
}
