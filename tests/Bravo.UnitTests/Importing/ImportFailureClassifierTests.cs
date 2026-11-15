using Bravo.Application.Importing.Exceptions;
using Bravo.Application.Importing.Services;
using FluentAssertions;

namespace Bravo.UnitTests.Importing;

public sealed class ImportFailureClassifierTests
{
    [Fact]
    public void TransientExceptionIsRetryable()
    {
        ImportFailureClassifier.IsTransient(new ImportTransientException("Temporary outage."))
            .Should().BeTrue();
    }

    [Fact]
    public void PermanentExceptionIsNotRetryable()
    {
        ImportFailureClassifier.IsTransient(new ImportPermanentException("Invalid source."))
            .Should().BeFalse();
    }

    [Fact]
    public void LeaseLossIsNotRetryable()
    {
        ImportFailureClassifier.IsTransient(new ImportLeaseLostException("Lease lost."))
            .Should().BeFalse();
    }

    [Fact]
    public void RetryAfterIsExtractedFromTransientException()
    {
        var retryAfter = TimeSpan.FromSeconds(12);

        ImportFailureClassifier.GetRetryAfter(new ImportTransientException("Rate limited.", retryAfter))
            .Should().Be(retryAfter);
    }

    [Fact]
    public void UnknownExceptionIsNotRetryable()
    {
        ImportFailureClassifier.IsTransient(new InvalidOperationException("Unknown failure."))
            .Should().BeFalse();
    }
}
