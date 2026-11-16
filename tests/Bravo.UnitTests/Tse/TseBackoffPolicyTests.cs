using Bravo.Infrastructure.Tse.Options;
using Bravo.Infrastructure.Tse.Resilience;
using Microsoft.Extensions.Options;
using FluentAssertions;

namespace Bravo.UnitTests.Tse;

public sealed class TseBackoffPolicyTests
{
    private static TseBackoffPolicy Create(int baseDelayMs = 500, int maxDelayMs = 30_000, int maxAttempts = 3)
        => new(Options.Create(new TseOptions
        {
            RetryBaseDelayMs = baseDelayMs,
            RetryMaxDelayMs = maxDelayMs,
            MaxRetryAttempts = maxAttempts
        }));

    [Fact]
    public void RetryAfterOverridesBackoff()
    {
        var delay = Create().GetDelay(3, TimeSpan.FromSeconds(9));

        delay.Should().Be(TimeSpan.FromSeconds(9));
    }

    [Theory]
    [InlineData(1, 500)]
    [InlineData(2, 1_000)]
    [InlineData(3, 2_000)]
    public void DelayGrowsExponentiallyWithJitter(int attempt, int expectedMs)
    {
        var delay = Create().GetDelay(attempt, retryAfter: null);

        delay.TotalMilliseconds.Should().BeInRange(expectedMs * 0.85, expectedMs * 1.15);
    }

    [Fact]
    public void DelayIsCapped()
    {
        var delay = Create(maxDelayMs: 5_000).GetDelay(20, retryAfter: null);

        delay.TotalMilliseconds.Should().BeInRange(5_000 * 0.85, 5_000 * 1.15);
    }

    [Fact]
    public void MaxAttemptsIsAtLeastOne()
    {
        Create(maxAttempts: 0).MaxAttempts.Should().Be(1);
    }
}
