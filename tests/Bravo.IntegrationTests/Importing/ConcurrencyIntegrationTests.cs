using Bravo.IntegrationTests.Configurations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.IntegrationTests.Importing;

[Collection("Database")]
public sealed class ConcurrencyIntegrationTests(PostgresContainerFixture pg) : ImportingTestBase(pg)
{
    [Fact]
    public async Task TwoWorkersCannotClaimTheSameJob()
    {
        var jobId = await CreateJobAsync();

        var first = await ClaimAsync(jobId, "worker-a");
        var second = await ClaimAsync(jobId, "worker-b");

        first.Should().NotBeNull();
        second.Should().BeNull();
        first!.LeaseOwner.Should().Be("worker-a");
    }

    [Fact]
    public async Task ExpiredLeaseCanBeReclaimedByAnotherWorker()
    {
        var jobId = await CreateJobAsync();

        var first = await ClaimAsync(jobId, "worker-a", leaseSeconds: 1);
        first.Should().NotBeNull();

        await Task.Delay(TimeSpan.FromMilliseconds(1_300));

        var second = await ClaimAsync(jobId, "worker-b");

        second.Should().NotBeNull();
        second!.LeaseOwner.Should().Be("worker-b");
        second.AttemptCount.Should().Be(first!.AttemptCount + 1);
    }

    [Fact]
    public async Task RenewalKeepsTheLeaseAndReportsOwnership()
    {
        var jobId = await CreateJobAsync();
        await ClaimAsync(jobId, "worker-a", leaseSeconds: 1);

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var jobs = scope.ServiceProvider.GetRequiredService<Bravo.Domain.Importing.Repositories.IImportJobRepository>();

        (await jobs.RenewLeaseAsync(jobId, "worker-a", TimeSpan.FromSeconds(60)))
            .Should().BeTrue();

        (await jobs.RenewLeaseAsync(jobId, "worker-b", TimeSpan.FromSeconds(60)))
            .Should().BeFalse();
    }

    [Fact]
    public async Task OverlappingScopeCannotStartTwice()
    {
        var scope = UniqueScope("scope");

        var first = NewJob(scope: scope);
        var duplicate = NewJob(scope: scope);
        var other = NewJob(scope: UniqueScope("other"));

        (await TryCreateJobAsync(first)).Should().BeTrue();
        (await TryCreateJobAsync(duplicate)).Should().BeFalse();
        (await TryCreateJobAsync(other)).Should().BeTrue();
    }

    [Fact]
    public async Task ConcurrentCreatesForTheSameScopeAllowOnlyOneWinner()
    {
        var scope = UniqueScope("race");
        var attempts = Enumerable.Range(0, 8).Select(_ => NewJob(scope: scope)).ToArray();

        var results = await Task.WhenAll(attempts.Select(job => TryCreateJobAsync(job)));

        results.Count(created => created).Should().Be(1);
    }

    [Fact]
    public async Task ParallelismIsBoundedByTheJobSetting()
    {
        Source.Operations = 8;
        Source.OperationDelayMilliseconds = 80;

        var jobId = await CreateJobAsync(maxParallelism: 3);

        await RunJobAsync(jobId);

        var run = Source.Runs.Single();

        run.MaxObservedParallelism.Should().BeGreaterThan(1);
        run.MaxObservedParallelism.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task ParallelismOfOneExecutesOperationsSequentially()
    {
        Source.Operations = 4;

        var jobId = await CreateJobAsync(maxParallelism: 1);

        await RunJobAsync(jobId);

        Source.Runs.Single().MaxObservedParallelism.Should().Be(1);
    }
}
