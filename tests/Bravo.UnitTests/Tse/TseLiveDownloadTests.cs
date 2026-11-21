using Bravo.Infrastructure.Tse.Clients;
using Bravo.Infrastructure.Tse.Extraction;
using Bravo.Infrastructure.Tse.Options;
using Bravo.Infrastructure.Tse.RateLimiting;
using Bravo.Infrastructure.Tse.Resilience;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bravo.UnitTests.Tse;

public sealed class TseLiveDownloadTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"tse-live-{Guid.NewGuid():N}");

    public TseLiveDownloadTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task DownloadsAndExtractsRealTseArchive()
    {
        if (Environment.GetEnvironmentVariable("BRAVO_LIVE_TSE_TESTS") != "1")
            return;

        var options = new TseOptions { TempDirectory = _root };
        var value = Options.Create(options);

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(options.BaseAddress),
            Timeout = TimeSpan.FromMinutes(5)
        };

        var client = new TseHttpClient(
            httpClient,
            new TseRateLimiter(value),
            new TseBackoffPolicy(value),
            value,
            NullLogger<TseHttpClient>.Instance);

        var downloaded = await client.DownloadCandidateArchiveAsync(2022, _root);

        downloaded.SizeBytes.Should().BeGreaterThan(1_000_000);
        downloaded.Sha256.Should().HaveLength(64);
        File.Exists(downloaded.FilePath).Should().BeTrue();

        var extractor = new TseArchiveExtractor(value, NullLogger<TseArchiveExtractor>.Instance);

        var files = extractor.Extract(
            downloaded.FilePath,
            Path.Combine(_root, "out"),
            entryFilter: name => name.EndsWith("_AC.csv", StringComparison.OrdinalIgnoreCase));

        files.Should().ContainSingle();
        new FileInfo(files[0].FilePath).Length.Should().BeGreaterThan(10_000);
    }
}
