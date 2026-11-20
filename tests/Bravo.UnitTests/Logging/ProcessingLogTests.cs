using Bravo.Application.Logging;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace Bravo.UnitTests.Logging;

public sealed class ProcessingLogTests
{
    private readonly TestLogger _logger = new();

    [Fact]
    public void DownloadEventsEmitExpectedProperties()
    {
        var retryEx = new HttpRequestException("429 Too Many Requests");
        var failedEx = new HttpRequestException("500 Internal Server Error");

        ProcessingLog.DownloadStarted(_logger, "https://example.com/file.zip");
        ProcessingLog.DownloadCompleted(_logger, "https://example.com/file.zip", 1024L, "sha256-hash");
        ProcessingLog.DownloadRetrying(_logger, "https://example.com/file.zip", 500.0, 1, 3, retryEx);
        ProcessingLog.DownloadFailed(_logger, "https://example.com/file.zip", 3, failedEx);

        var entries = _logger.Entries.OrderBy(x => x.EventId.Id).ToArray();
        entries.Should().HaveCount(4);

        entries[0].EventId.Id.Should().Be(1201);
        entries[0].LogLevel.Should().Be(LogLevel.Debug);

        entries[1].EventId.Id.Should().Be(1202);
        entries[1].LogLevel.Should().Be(LogLevel.Information);
        entries[1].Properties["SizeBytes"].Should().Be(1024L);
        entries[1].Properties["Sha256"].Should().Be("sha256-hash");

        entries[2].EventId.Id.Should().Be(1203);
        entries[2].LogLevel.Should().Be(LogLevel.Warning);
        entries[2].Exception.Should().BeSameAs(retryEx);

        entries[3].EventId.Id.Should().Be(1204);
        entries[3].LogLevel.Should().Be(LogLevel.Error);
        entries[3].Exception.Should().BeSameAs(failedEx);
    }

    [Fact]
    public void ExtractionAndParsingEventsEmitExpectedProperties()
    {
        ProcessingLog.ExtractionStarted(_logger, "/tmp/archive.zip", "/tmp/extracted");
        ProcessingLog.ExtractionCompleted(_logger, "/tmp/archive.zip", 10, 50000L);
        ProcessingLog.EntryExtracted(_logger, "data.csv", 5000L);
        ProcessingLog.ParsingStarted(_logger, "/tmp/extracted/data.csv", "cand-2022-v1");
        ProcessingLog.ParsingCompleted(_logger, "/tmp/extracted/data.csv", 100);
        ProcessingLog.WorkspaceCleaned(_logger, "/tmp/workspace-1");

        var entries = _logger.Entries.OrderBy(x => x.EventId.Id).ToArray();
        entries.Should().HaveCount(6);

        entries[0].EventId.Id.Should().Be(1205);
        entries[1].EventId.Id.Should().Be(1206);
        entries[1].Properties["FileCount"].Should().Be(10);
        entries[2].EventId.Id.Should().Be(1207);
        entries[2].Properties["Bytes"].Should().Be(5000L);
        entries[3].EventId.Id.Should().Be(1208);
        entries[3].Properties["LayoutVersion"].Should().Be("cand-2022-v1");
        entries[4].EventId.Id.Should().Be(1209);
        entries[4].Properties["RecordCount"].Should().Be(100);
        entries[5].EventId.Id.Should().Be(1210);
        entries[5].LogLevel.Should().Be(LogLevel.Information);
    }

    [Fact]
    public void ScopeFailuresPreserveExceptions()
    {
        var ex1 = new InvalidOperationException("Reference error");
        var ex2 = new InvalidOperationException("Candidate error");
        var ex3 = new InvalidOperationException("Result error");

        ProcessingLog.ReferenceImportFailed(_logger, 2022, ex1);
        ProcessingLog.CandidateImportFailed(_logger, 2022, ex2);
        ProcessingLog.ResultScopeImportFailed(_logger, 2022, "AC", ex3);

        var entries = _logger.Entries.OrderBy(x => x.EventId.Id).ToArray();
        entries.Should().HaveCount(3);

        entries[0].EventId.Id.Should().Be(1220);
        entries[0].Exception.Should().BeSameAs(ex1);
        entries[0].Properties["Year"].Should().Be(2022);

        entries[1].EventId.Id.Should().Be(1221);
        entries[1].Exception.Should().BeSameAs(ex2);

        entries[2].EventId.Id.Should().Be(1222);
        entries[2].Exception.Should().BeSameAs(ex3);
        entries[2].Properties["State"].Should().Be("AC");
    }
}
