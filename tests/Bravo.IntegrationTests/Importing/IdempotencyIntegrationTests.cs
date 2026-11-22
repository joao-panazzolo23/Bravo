using Bravo.Application.Imports.Common.Exceptions;
using Bravo.Domain.Importing.Enums;
using Bravo.Domain.Importing.Models;
using Bravo.IntegrationTests.Configurations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.IntegrationTests.Importing;

[Collection("Database")]
public sealed class IdempotencyIntegrationTests(PostgresContainerFixture pg) : ImportingTestBase(pg)
{
    [Fact]
    public async Task RerunningTheSameJobDoesNotDuplicateFailedRecords()
    {
        var jobId = await CreateJobAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var failedRecords = scope.ServiceProvider
            .GetRequiredService<Bravo.Domain.Importing.Repositories.IImportFailedRecordRepository>();

        var rejected = new[]
        {
            new ImportRejectedRecord(
                "source:row:1",
                "malformed payload",
                ImportErrorCategory.Deserialization,
                "The source row could not be parsed."),
            new ImportRejectedRecord(
                "source:row:2",
                null,
                ImportErrorCategory.Validation,
                "The source row was rejected.")
        };

        await failedRecords.UpsertAsync(jobId, rejected);
        await failedRecords.UpsertAsync(jobId, rejected);

        var stored = await GetFailedRecordsAsync(jobId);

        stored.Should().HaveCount(2);
        stored.Should().OnlyContain(record => record.AttemptCount == 2);
    }

    [Fact]
    public async Task RerunningAnImportUpdatesTheSameFailedRecord()
    {
        var jobId = await CreateJobAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var failedRecords = scope.ServiceProvider
            .GetRequiredService<Bravo.Domain.Importing.Repositories.IImportFailedRecordRepository>();

        await failedRecords.UpsertAsync(jobId, [
            new ImportRejectedRecord(
                "source:row:1", null, ImportErrorCategory.Source, "First error.")
        ]);

        await failedRecords.UpsertAsync(jobId, [
            new ImportRejectedRecord(
                "source:row:1", null, ImportErrorCategory.Source, "Corrected error.")
        ]);

        var stored = await GetFailedRecordsAsync(jobId);

        stored.Should().ContainSingle();
        stored.Single().ErrorDescription.Should().Be("Corrected error.");
        stored.Single().AttemptCount.Should().Be(2);
    }

    [Fact]
    public async Task FullRestartProducesTheSameFinalState()
    {
        Source.Operations = 5;
        Source.RecordsPerOperation = 4;

        var scope = UniqueScope("restart");

        var firstJob = await CreateJobAsync(scope: scope);
        var first = await RunJobAsync(firstJob);

        first!.Status.Should().Be(ImportJobStatus.Completed);
        first.TotalProcessedRecords.Should().Be(20);

        // A restart re-executes the whole import from the beginning. Because the job is
        // completed it no longer blocks the scope, so a second full run is allowed and must
        // converge on the same counters instead of accumulating.
        var secondJob = await CreateJobAsync(scope: scope);
        var second = await RunJobAsync(secondJob);

        second!.Status.Should().Be(ImportJobStatus.Completed);
        second.TotalProcessedRecords.Should().Be(20);
        second.CompletedOperations.Should().Be(5);
        Source.ExecutionCount.Should().Be(2);
    }

    [Fact]
    public async Task RejectedRecordsFromDifferentJobsStayIsolated()
    {
        var firstJob = await CreateJobAsync(scope: UniqueScope("first"));
        var secondJob = await CreateJobAsync(scope: UniqueScope("second"));

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var failedRecords = scope.ServiceProvider
            .GetRequiredService<Bravo.Domain.Importing.Repositories.IImportFailedRecordRepository>();

        var rejected = new[]
        {
            new ImportRejectedRecord(
                "source:row:1", null, ImportErrorCategory.Validation, "Rejected.")
        };

        await failedRecords.UpsertAsync(firstJob, rejected);
        await failedRecords.UpsertAsync(secondJob, rejected);

        (await GetFailedRecordsAsync(firstJob)).Should().ContainSingle();
        (await GetFailedRecordsAsync(secondJob)).Should().ContainSingle();
    }
}
