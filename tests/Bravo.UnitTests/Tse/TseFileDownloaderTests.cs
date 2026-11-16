using System.Net;
using System.Security.Cryptography;
using System.Text;
using Bravo.Infrastructure.Tse.Downloads;
using Bravo.Infrastructure.Tse.Options;
using Bravo.Infrastructure.Tse.RateLimiting;
using Bravo.Infrastructure.Tse.Resilience;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bravo.UnitTests.Tse;

public sealed class TseFileDownloaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"tse-dl-{Guid.NewGuid():N}");

    public TseFileDownloaderTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, int, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        private int _calls;

        public int Calls => _calls;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(responder(request, Interlocked.Increment(ref _calls)));
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://cdn.tse.jus.br/")
        };
    }

    private TseFileDownloader CreateDownloader(
        HttpMessageHandler handler,
        Action<TseOptions>? configure = null)
    {
        var options = new TseOptions
        {
            TempDirectory = _root,
            RetryBaseDelayMs = 1,
            RetryMaxDelayMs = 2,
            MaxRetryAttempts = 3
        };

        configure?.Invoke(options);

        var value = Options.Create(options);

        return new TseFileDownloader(
            new StubFactory(handler),
            new TseRateLimiter(value.Value),
            new TseBackoffPolicy(value.Value),
            value,
            NullLogger<TseFileDownloader>.Instance);
    }

    private static HttpResponseMessage Ok(string content)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.Latin1)
        };

    [Fact]
    public async Task DownloadsFileAndComputesSha256()
    {
        var payload = "A;B\n1;2\n";
        var downloader = CreateDownloader(new StubHandler((_, _) => Ok(payload)));

        var result = await downloader.DownloadAsync("data.zip", "data.zip");

        result.FileName.Should().Be("data.zip");
        result.SizeBytes.Should().Be(Encoding.Latin1.GetByteCount(payload));
        result.Sha256.Should().Be(
            Convert.ToHexStringLower(SHA256.HashData(Encoding.Latin1.GetBytes(payload))));
        File.Exists(result.FilePath).Should().BeTrue();
        File.Exists(result.FilePath + ".part").Should().BeFalse();
    }

    [Fact]
    public async Task RetriesTransientServerErrorThenSucceeds()
    {
        var downloader = CreateDownloader(new StubHandler((_, call) => call < 3
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : Ok("ok")));

        var result = await downloader.DownloadAsync("a.zip", "a.zip");

        File.ReadAllText(result.FilePath).Should().Be("ok");
    }

    [Fact]
    public async Task GivesUpAfterMaxAttempts()
    {
        var handler = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var downloader = CreateDownloader(handler);

        var act = () => downloader.DownloadAsync("a.zip", "a.zip");

        await act.Should().ThrowAsync<TseDownloadException>();
        handler.Calls.Should().Be(3);
    }

    [Fact]
    public async Task DoesNotRetryClientError()
    {
        var handler = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound));
        var downloader = CreateDownloader(handler);

        var act = () => downloader.DownloadAsync("a.zip", "a.zip");

        await act.Should().ThrowAsync<TseDownloadException>();
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task RejectsDeclaredSizeAboveLimit()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("x")
        };

        response.Content.Headers.ContentLength = 10_000;

        var downloader = CreateDownloader(
            new StubHandler((_, _) => response),
            options => options.MaxDownloadBytes = 1_000);

        var act = () => downloader.DownloadAsync("big.zip", "big.zip");

        await act.Should().ThrowAsync<TseDownloadException>().WithMessage("*exceeds*");
    }

    [Fact]
    public async Task LeavesNoPartialFileBehindOnFailure()
    {
        var downloader = CreateDownloader(
            new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound)));

        var act = () => downloader.DownloadAsync("a.zip", "a.zip");

        await act.Should().ThrowAsync<TseDownloadException>();
        Directory.GetFiles(_root, "*.part").Should().BeEmpty();
    }

    [Fact]
    public async Task HonorsCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var downloader = CreateDownloader(new StubHandler((_, _) => Ok("x")));

        var act = () => downloader.DownloadAsync("a.zip", "a.zip", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
