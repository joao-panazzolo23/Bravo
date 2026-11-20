using Bravo.Application.Logging;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace Bravo.UnitTests.Logging;

public sealed class WorkerLogTests
{
    private readonly TestLogger _logger = new();

    [Fact]
    public void WorkerLifecycleEventsEmitExpectedDetails()
    {
        WorkerLog.WorkerStarted(_logger, "worker-1", 5, 2);
        WorkerLog.WorkerDisabled(_logger);
        WorkerLog.WorkerIdle(_logger, "worker-1", 2);
        WorkerLog.WorkerStopped(_logger, "worker-1");

        var entries = _logger.Entries.OrderBy(x => x.EventId.Id).ToArray();
        entries.Should().HaveCount(4);

        entries[0].EventId.Id.Should().Be(1101);
        entries[0].LogLevel.Should().Be(LogLevel.Information);
        entries[0].Properties["WorkerId"].Should().Be("worker-1");

        entries[1].EventId.Id.Should().Be(1102);
        entries[1].LogLevel.Should().Be(LogLevel.Information);

        entries[2].EventId.Id.Should().Be(1103);
        entries[2].LogLevel.Should().Be(LogLevel.Information);

        entries[3].EventId.Id.Should().Be(1104);
        entries[3].LogLevel.Should().Be(LogLevel.Debug);
        entries[3].Properties["RunningJobs"].Should().Be(2);
    }

    [Fact]
    public void WorkerFailureEventsPreserveExceptions()
    {
        var jobId = Guid.NewGuid();
        var claimEx = new InvalidOperationException("DB down");
        var heartbeatEx = new TimeoutException("Heartbeat timed out");
        var renewEx = new InvalidOperationException("Lease expired");
        var pollingEx = new InvalidOperationException("Poll failed");
        var releaseEx = new InvalidOperationException("Release failed");

        WorkerLog.WorkerClaimFailed(_logger, "worker-1", jobId, claimEx);
        WorkerLog.WorkerLeaseLost(_logger, jobId, "worker-1");
        WorkerLog.WorkerHeartbeatFailed(_logger, jobId, heartbeatEx);
        WorkerLog.WorkerLeaseRenewFailed(_logger, jobId, renewEx);
        WorkerLog.WorkerAbandonedJobsRecovered(_logger, 3, "machine-1");
        WorkerLog.WorkerPollingFailed(_logger, pollingEx);
        WorkerLog.WorkerReleaseAbandonedFailed(_logger, releaseEx);
        WorkerLog.UnregisteredSource(_logger, jobId, "UNKNOWN");

        var entries = _logger.Entries.OrderBy(x => x.EventId.Id).ToArray();
        entries.Should().HaveCount(8);

        entries[0].EventId.Id.Should().Be(1105);
        entries[0].Exception.Should().BeSameAs(claimEx);

        entries[1].EventId.Id.Should().Be(1106);
        entries[1].LogLevel.Should().Be(LogLevel.Warning);

        entries[2].EventId.Id.Should().Be(1107);
        entries[2].Exception.Should().BeSameAs(heartbeatEx);

        entries[3].EventId.Id.Should().Be(1108);
        entries[3].Exception.Should().BeSameAs(renewEx);

        entries[4].EventId.Id.Should().Be(1109);
        entries[4].LogLevel.Should().Be(LogLevel.Warning);
        entries[4].Properties["Count"].Should().Be(3);

        entries[5].EventId.Id.Should().Be(1110);
        entries[5].Exception.Should().BeSameAs(pollingEx);

        entries[6].EventId.Id.Should().Be(1111);
        entries[6].Exception.Should().BeSameAs(releaseEx);

        entries[7].EventId.Id.Should().Be(1112);
        entries[7].LogLevel.Should().Be(LogLevel.Error);
        entries[7].Properties["Source"].Should().Be("UNKNOWN");
    }
}
