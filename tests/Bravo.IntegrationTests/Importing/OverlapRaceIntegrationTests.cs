using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Enums;
using Bravo.IntegrationTests.Configurations;
using FluentAssertions;

namespace Bravo.IntegrationTests.Importing;

[Collection("Database")]
public sealed class OverlapRaceIntegrationTests(PostgresContainerFixture pg) : ImportingTestBase(pg)
{
    [Fact]
    public async Task ConcurrentCreatesForTheSameScopeAllowOnlyOneWinner()
    {
        var attempts = Enumerable.Range(0, 8)
            .Select(_ => CreateOverlapJob("RaceOverlap"))
            .ToArray();

        var results = await Task.WhenAll(attempts.Select(job => TryCreateJobAsync(job, "states")));

        results.Count(created => created).Should().Be(1);
    }

    private static ImportJob CreateOverlapJob(string importType)
        => ImportJob.Create(importType, ImportConflictStrategy.DoNothing, 10, 3, 60);
}
