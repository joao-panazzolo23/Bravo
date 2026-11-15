using Bravo.Application.Importing.Models;
using Bravo.Domain.Importing.Enums;
using Bravo.IntegrationTests.Configurations;
using FluentAssertions;

namespace Bravo.IntegrationTests.Importing;

[Collection("Database")]
public sealed class IdempotencyIntegrationTests(PostgresContainerFixture pg) : ImportingTestBase(pg)
{
    [Fact]
    public async Task ReprocessingIdenticalBatchDoesNotCreateDuplicates()
    {
        Source.Add(CreateState(51), CreateState(52), CreateState(53));
        var firstJob = await CreateJobAsync(batchSize: 2);
        await RunJobAsync(firstJob.Id);

        Source.Reset();
        Source.Add(CreateState(51), CreateState(52), CreateState(53));
        var secondJob = await CreateJobAsync(batchSize: 2);
        await RunJobAsync(secondJob.Id);

        (await CountStatesAsync()).Should().Be(3);
        (await GetJobAsync(secondJob.Id))!.Status.Should().Be(ImportJobStatus.Completed);
    }

    [Fact]
    public async Task ReimportingUpdatesExistingRecords()
    {
        Source.Add(CreateState(61, name: "Original"));
        var firstJob = await CreateJobAsync(batchSize: 1);
        await RunJobAsync(firstJob.Id);

        Source.Reset();
        Source.Add(CreateState(61, name: "Replacement"));
        var secondJob = await CreateJobAsync(batchSize: 1);
        await RunJobAsync(secondJob.Id);

        (await GetStateNameAsync(61)).Should().Be("Replacement");
        (await CountStatesAsync()).Should().Be(1);
    }

    [Fact]
    public async Task InvalidRecordsAreStoredAsFailedRecordsAndDoNotStopTheJob()
    {
        Source.Add(CreateState(91), CreateState(92), CreateState(93));
        Source.Reject(new ImportRejectedRecord(
            "source:row:1",
            "malformed payload",
            ImportErrorCategory.Deserialization,
            "The source row could not be parsed."));
        Validator.Reject(92);

        var job = await CreateJobAsync(batchSize: 3);

        await RunJobAsync(job.Id);

        var completedJob = await GetJobAsync(job.Id);
        var failedRecords = await GetFailedRecordsAsync(job.Id);

        completedJob!.Status.Should().Be(ImportJobStatus.CompletedWithErrors);
        completedJob.TotalProcessedRecords.Should().Be(2);
        completedJob.TotalFailedRecords.Should().Be(2);
        failedRecords.Should().HaveCount(2);
        failedRecords.Should().ContainSingle(record => record.SourceIdentifier == "source:row:1");
        failedRecords.Should().ContainSingle(record => record.ErrorCategory == ImportErrorCategory.Validation);
        (await CountStatesAsync()).Should().Be(2);
    }
}
