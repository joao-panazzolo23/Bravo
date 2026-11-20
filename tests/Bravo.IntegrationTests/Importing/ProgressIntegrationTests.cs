using Bravo.Domain.Importing.Models;
using Bravo.Domain.Importing.Enums;
using Bravo.IntegrationTests.Configurations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.IntegrationTests.Importing;

[Collection("Database")]
public sealed class ProgressIntegrationTests(PostgresContainerFixture pg) : ImportingTestBase(pg)
{
    [Fact]
    public async Task ProgressEndpointReportsJobLevelCounters()
    {
        Source.Operations = 6;
        Source.RecordsPerOperation = 4;

        var jobId = await CreateJobAsync(maxParallelism: 2);
        await RunJobAsync(jobId);

        var response = await Client.GetAsync($"/api/v1/import/{jobId}/progress");

        response.IsSuccessStatusCode.Should().BeTrue();

        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("\"status\":\"Completed\"");
        body.Should().Contain("\"workUnits\"");
        body.Should().Contain("\"records\"");
        body.Should().Contain("\"throughput\"");
    }

    [Fact]
    public async Task StatusEndpointKeepsTheCreatedJobContract()
    {
        var jobId = await CreateJobAsync();

        var response = await Client.GetAsync($"/api/v1/import/{jobId}");

        response.IsSuccessStatusCode.Should().BeTrue();

        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("\"id\"");
        body.Should().Contain("\"status\"");
        body.Should().Contain("\"batchSize\"");
        body.Should().Contain("\"startedAt\"");
    }

    [Fact]
    public async Task UnknownJobReturnsNotFound()
    {
        var response = await Client.GetAsync($"/api/v1/import/{Guid.NewGuid()}/progress");

        ((int)response.StatusCode).Should().Be(404);
    }

    [Fact]
    public async Task ConcurrentProgressWritesKeepTheLatestAbsoluteValues()
    {
        var jobId = await CreateJobAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var jobs = scope.ServiceProvider
            .GetRequiredService<Bravo.Domain.Importing.Repositories.IImportJobRepository>();

        await jobs.ClaimAsync(jobId, "worker-a", TimeSpan.FromSeconds(60));

        var writes = Enumerable.Range(1, 20)
            .Select(value => jobs.ReportProgressAsync(
                jobId,
                "worker-a",
                new ImportJobProgress(
                    ImportStage.Reading,
                    100,
                    value,
                    0,
                    value * 10L,
                    0,
                    $"Operation {value}")))
            .ToArray();

        var results = await Task.WhenAll(writes);

        results.Should().OnlyContain(written => written);

        var job = await GetJobAsync(jobId);

        // Every write stores absolute values, so the final counters are one of the reported
        // snapshots rather than a sum or a torn mix.
        job!.CompletedOperations.Should().BeInRange(1, 20);
        job.TotalProcessedRecords.Should().Be(job.CompletedOperations * 10L);
    }
}
