using Bravo.Application.Logging;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace Bravo.UnitTests.Logging;

public sealed class ImportLogTests
{
    private readonly TestLogger _logger = new();

    [Fact]
    public void JobStartedEmitsExpectedEventAndProperties()
    {
        var jobId = Guid.NewGuid();

        ImportLog.JobStarted(_logger, jobId, "TSE", "TSE:electoral", 1);

        var entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.EventId.Id.Should().Be(1001);
        entry.LogLevel.Should().Be(LogLevel.Information);
        entry.Properties["JobId"].Should().Be(jobId);
        entry.Properties["Source"].Should().Be("TSE");
        entry.Properties["Scope"].Should().Be("TSE:electoral");
        entry.Properties["Attempt"].Should().Be(1);
    }

    [Fact]
    public void JobCompletedEmitsExpectedEventAndProperties()
    {
        var jobId = Guid.NewGuid();

        ImportLog.JobCompleted(_logger, jobId, 1500L, 0L);

        var entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.EventId.Id.Should().Be(1002);
        entry.LogLevel.Should().Be(LogLevel.Information);
        entry.Properties["JobId"].Should().Be(jobId);
        entry.Properties["ProcessedRecords"].Should().Be(1500L);
        entry.Properties["FailedRecords"].Should().Be(0L);
    }

    [Fact]
    public void JobCompletedWithErrorsEmitsWarning()
    {
        var jobId = Guid.NewGuid();

        ImportLog.JobCompletedWithErrors(_logger, jobId, 1400L, 100L);

        var entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.EventId.Id.Should().Be(1003);
        entry.LogLevel.Should().Be(LogLevel.Warning);
        entry.Properties["JobId"].Should().Be(jobId);
        entry.Properties["ProcessedRecords"].Should().Be(1400L);
        entry.Properties["FailedRecords"].Should().Be(100L);
    }

    [Fact]
    public void JobCancelledEmitsWarning()
    {
        var jobId = Guid.NewGuid();

        ImportLog.JobCancelled(_logger, jobId);

        var entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.EventId.Id.Should().Be(1004);
        entry.LogLevel.Should().Be(LogLevel.Warning);
        entry.Properties["JobId"].Should().Be(jobId);
    }

    [Fact]
    public void JobStoppedEmitsInformation()
    {
        var jobId = Guid.NewGuid();

        ImportLog.JobStopped(_logger, jobId);

        var entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.EventId.Id.Should().Be(1005);
        entry.LogLevel.Should().Be(LogLevel.Information);
        entry.Properties["JobId"].Should().Be(jobId);
    }

    [Fact]
    public void JobReleasedEmitsInformation()
    {
        var jobId = Guid.NewGuid();

        ImportLog.JobReleased(_logger, jobId);

        var entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.EventId.Id.Should().Be(1006);
        entry.LogLevel.Should().Be(LogLevel.Information);
        entry.Properties["JobId"].Should().Be(jobId);
    }

    [Fact]
    public void JobFailedPreservesExceptionAndProperties()
    {
        var jobId = Guid.NewGuid();
        var retryAt = DateTime.UtcNow.AddMinutes(5);
        var exception = new InvalidOperationException("Connection aborted");

        ImportLog.JobFailed(_logger, jobId, "TSE", 1, 3, retryAt, exception);

        var entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.EventId.Id.Should().Be(1007);
        entry.LogLevel.Should().Be(LogLevel.Error);
        entry.Exception.Should().BeSameAs(exception);
        entry.Properties["JobId"].Should().Be(jobId);
        entry.Properties["Source"].Should().Be("TSE");
        entry.Properties["Attempt"].Should().Be(1);
        entry.Properties["MaxAttempts"].Should().Be(3);
        entry.Properties["RetryAt"].Should().Be(retryAt);
    }
}
