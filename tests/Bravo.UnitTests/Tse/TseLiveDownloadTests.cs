using Bravo.Infrastructure.Tse.Downloads;
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

        var factory = new Factory();

        var downloader = new TseFileDownloader(
            factory,
            new TseRateLimiter(value),
            new TseBackoffPolicy(value),
            value,
            NullLogger<TseFileDownloader>.Instance);

        var downloaded = await downloader.DownloadAsync(
            "estatistica/sead/odsele/consulta_cand/consulta_cand_2022.zip",
            "consulta_cand_2022.zip");

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

    private sealed class Factory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new()
        {
            BaseAddress = new Uri("https://cdn.tse.jus.br/"),
            Timeout = TimeSpan.FromMinutes(5)
        };
    }
}
