using Bravo.Application.Imports.Common.Options;
using Bravo.Application.Imports.Common.Policies;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Bravo.UnitTests.Importing;

public sealed class ImportRetryPolicyTests
{
    private static ImportRetryPolicy CreatePolicy(int baseDelayMs = 500, int maxDelayMs = 30_000)
        => new(Options.Create(new ImportEngineOptions
        {
            RetryBaseDelayMilliseconds = baseDelayMs,
            RetryMaxDelayMilliseconds = maxDelayMs
        }));

    [Fact]
    public void RetryAfterOverridesCalculatedBackoff()
    {
        var delay = CreatePolicy().GetDelay(3, TimeSpan.FromSeconds(7));

        delay.Should().Be(TimeSpan.FromSeconds(7));
    }

    [Theory]
    [InlineData(1, 500)]
    [InlineData(2, 1_000)]
    [InlineData(3, 2_000)]
    public void BackoffGrowsExponentially(int attempt, int expectedBaseDelayMs)
    {
        var delay = CreatePolicy().GetDelay(attempt, retryAfter: null);

        delay.TotalMilliseconds.Should().BeInRange(
            expectedBaseDelayMs * 0.85,
            expectedBaseDelayMs * 1.15);
    }

    [Fact]
    public void BackoffIsCappedAtMaximumDelay()
    {
        var delay = CreatePolicy().GetDelay(20, retryAfter: null);

        delay.TotalMilliseconds.Should().BeInRange(30_000 * 0.85, 30_000 * 1.15);
    }
}
