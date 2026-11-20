using Bravo.Application.Logging;
using FluentAssertions;

namespace Bravo.UnitTests.Logging;

public sealed class LoggingScopeTests
{
    private readonly TestLogger _logger = new();

    [Fact]
    public void JobScopeCapturesOperationalContext()
    {
        var jobId = Guid.NewGuid();

        using (_logger.BeginJobScope(jobId, "TSE", "worker-1", "corr-123"))
        {
            ImportLog.JobCancelled(_logger, jobId);
        }

        var entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.Scopes.Should().ContainSingle();

        var scope = entry.Scopes[0].Should().BeAssignableTo<IReadOnlyList<KeyValuePair<string, object?>>>().Subject;
        var dictionary = scope.ToDictionary(x => x.Key, x => x.Value);

        dictionary["JobId"].Should().Be(jobId);
        dictionary["Source"].Should().Be("TSE");
        dictionary["WorkerId"].Should().Be("worker-1");
        dictionary["CorrelationId"].Should().Be("corr-123");
    }

    [Fact]
    public void WorkUnitScopeCapturesUnitContext()
    {
        var unitId = Guid.NewGuid();

        using (_logger.BeginWorkUnitScope(unitId, year: 2022, state: "AC", correlationId: "unit-corr"))
        {
            ImportLog.WorkUnitStarted(_logger, unitId, "cand:2022:AC");
        }

        var entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.Scopes.Should().ContainSingle();

        var scope = entry.Scopes[0].Should().BeAssignableTo<IReadOnlyList<KeyValuePair<string, object?>>>().Subject;
        var dictionary = scope.ToDictionary(x => x.Key, x => x.Value);

        dictionary["WorkUnitId"].Should().Be(unitId);
        dictionary["Year"].Should().Be(2022);
        dictionary["State"].Should().Be("AC");
        dictionary["CorrelationId"].Should().Be("unit-corr");
    }

    [Fact]
    public void NestedScopesPropagateAllContextLevels()
    {
        var jobId = Guid.NewGuid();
        var unitId = Guid.NewGuid();

        using (_logger.BeginJobScope(jobId, "TSE", "worker-1"))
        {
            using (_logger.BeginWorkUnitScope(unitId, year: 2022, state: "SP"))
            {
                using (_logger.BeginOperationScope(year: 2022, state: "SP", correlationId: "op-1"))
                {
                    ProcessingLog.DownloadStarted(_logger, "https://tse.jus.br/file.zip");
                }
            }
        }

        var entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.Scopes.Should().HaveCount(3);
    }

    [Fact]
    public void ScopeDisposalRemovesScopeFromSubsequentLogs()
    {
        var jobId = Guid.NewGuid();

        using (_logger.BeginJobScope(jobId, "TSE"))
        {
            ImportLog.JobStarted(_logger, jobId, "TSE", "all", 1);
        }

        ImportLog.JobStopped(_logger, jobId);

        _logger.Entries.Should().HaveCount(2);
        _logger.Entries.First(x => x.EventId.Id == 1001).Scopes.Should().ContainSingle();
        _logger.Entries.First(x => x.EventId.Id == 1005).Scopes.Should().BeEmpty();
    }

    [Fact]
    public async Task ParallelOperationsMaintainContextIsolation()
    {
        var barrier = new Barrier(2);
        var task1Started = new TaskCompletionSource();
        var task2Started = new TaskCompletionSource();

        var task1 = Task.Run(async () =>
        {
            using (_logger.BeginOperationScope(year: 2020, state: "RJ"))
            {
                task1Started.SetResult();
                barrier.SignalAndWait();
                await Task.Delay(10);
                ProcessingLog.DownloadStarted(_logger, "url-2020");
            }
        });

        var task2 = Task.Run(async () =>
        {
            using (_logger.BeginOperationScope(year: 2022, state: "SP"))
            {
                task2Started.SetResult();
                barrier.SignalAndWait();
                await Task.Delay(10);
                ProcessingLog.DownloadStarted(_logger, "url-2022");
            }
        });

        await Task.WhenAll(task1, task2);

        _logger.Entries.Should().HaveCount(2);

        var entry2020 = _logger.Entries.First(x => (string)x.Properties["Url"]! == "url-2020");
        var scope2020 = (IReadOnlyList<KeyValuePair<string, object?>>)entry2020.Scopes.Single()!;
        var dict2020 = scope2020.ToDictionary(x => x.Key, x => x.Value);
        dict2020["Year"].Should().Be(2020);
        dict2020["State"].Should().Be("RJ");

        var entry2022 = _logger.Entries.First(x => (string)x.Properties["Url"]! == "url-2022");
        var scope2022 = (IReadOnlyList<KeyValuePair<string, object?>>)entry2022.Scopes.Single()!;
        var dict2022 = scope2022.ToDictionary(x => x.Key, x => x.Value);
        dict2022["Year"].Should().Be(2022);
        dict2022["State"].Should().Be("SP");
    }
}
