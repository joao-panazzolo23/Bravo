using Bravo.Application.Acquisition.Options;
using Bravo.Application.Acquisition.Providers;
using Bravo.Infrastructure.Acquisition.Http;
using Bravo.Infrastructure.Acquisition.RateLimiting;
using Bravo.Infrastructure.Acquisition.Resilience;
using Bravo.Infrastructure.Acquisition.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bravo.UnitTests.Acquisition;

public class DatasetFetcherTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bravo-tests", Guid.NewGuid().ToString("N"));

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        return Task.CompletedTask;
    }

    private DatasetFetcher CreateFetcher(HttpMessageHandler handler, ProviderOptions policy, out AcquisitionOptions options)
    {
        options = new AcquisitionOptions
        {
            TempDirectory = _root,
            MaxDownloadBytes = 1024 * 1024,
            Ibge = policy,
            Tse = policy,
            Fgv = policy
        };

        var httpClient = new HttpClient(handler);
        var limiters = new HostRateLimiterRegistry(Options.Create(options));
        var pipeline = ProviderResiliencePipelineFactory.Create(policy, NullLogger.Instance);
        var store = new TemporaryFileStore(Options.Create(options), NullLogger<TemporaryFileStore>.Instance);

        return new DatasetFetcher(
            httpClient,
            pipeline,
            limiters,
            store,
            policy,
            options,
            NullLogger.Instance);
    }

    private static ProviderOptions Policy(params string[] hosts) => new()
    {
        BaseAddress = $"https://{hosts[0]}/",
        MaxConcurrentRequests = 2,
        MinDelayBetweenRequestsMs = 0,
        MaxRetryAttempts = 2,
        RetryBaseDelayMs = 5,
        HttpTimeoutSeconds = 30,
        AllowedHosts = hosts
    };

    [Fact]
    public async Task DownloadAsync_ShouldStreamToDiskAndHashContent()
    {
        var payload = System.Text.Encoding.UTF8.GetBytes("coluna;valor\n1;2\n");
        var handler = new TestHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(payload)
        }));

        var fetcher = CreateFetcher(handler, Policy("cdn.tse.jus.br"), out _);
        var result = await fetcher.DownloadAsync(Guid.NewGuid(), "dataset", new Uri("https://cdn.tse.jus.br/a.zip"), null, CancellationToken.None);

        result.Should().NotBeNull();
        result!.SizeBytes.Should().Be(payload.Length);
        File.Exists(result.FilePath).Should().BeTrue();
        (await File.ReadAllBytesAsync(result.FilePath)).Should().Equal(payload);

        var expected = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(payload));
        result.Sha256.Should().Be(expected);
    }

    [Fact]
    public async Task DownloadAsync_ShouldSendConditionalHeadersAndHandleNotModified()
    {
        HttpRequestMessage? captured = null;
        var handler = new TestHttpMessageHandler(request =>
        {
            captured = request;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotModified));
        });

        var fetcher = CreateFetcher(handler, Policy("cdn.tse.jus.br"), out _);
        var conditional = new ConditionalRequest("\"abc\"", DateTimeOffset.UtcNow.AddDays(-1));
        var result = await fetcher.DownloadAsync(Guid.NewGuid(), "dataset", new Uri("https://cdn.tse.jus.br/a.zip"), conditional, CancellationToken.None);

        result.Should().BeNull();
        captured!.Headers.IfNoneMatch.ToString().Should().Be("\"abc\"");
        captured.Headers.IfModifiedSince.Should().NotBeNull();
    }

    [Fact]
    public async Task DownloadAsync_ShouldRejectHostOutsideAllowlist()
    {
        var handler = new TestHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)));
        var fetcher = CreateFetcher(handler, Policy("cdn.tse.jus.br"), out _);

        var act = async () => await fetcher.DownloadAsync(
            Guid.NewGuid(),
            "dataset",
            new Uri("https://evil.example.com/a.zip"),
            null,
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DownloadAsync_ShouldAbortWhenDownloadExceedsLimit()
    {
        var handler = new TestHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[2048])
        }));

        var fetcher = CreateFetcher(handler, Policy("cdn.tse.jus.br"), out var options);
        options.MaxDownloadBytes = 1024;

        var act = async () => await fetcher.DownloadAsync(
            Guid.NewGuid(),
            "dataset",
            new Uri("https://cdn.tse.jus.br/big.zip"),
            null,
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DownloadAsync_ShouldRetryOn429ThenSucceed()
    {
        var attempts = 0;
        var handler = new TestHttpMessageHandler(_ =>
        {
            attempts++;
            if (attempts == 1)
            {
                var tooMany = new HttpResponseMessage(System.Net.HttpStatusCode.TooManyRequests);
                tooMany.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMilliseconds(10));
                return Task.FromResult(tooMany);
            }

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3])
            });
        });

        var fetcher = CreateFetcher(handler, Policy("cdn.tse.jus.br"), out _);
        var result = await fetcher.DownloadAsync(Guid.NewGuid(), "dataset", new Uri("https://cdn.tse.jus.br/a.zip"), null, CancellationToken.None);

        attempts.Should().Be(2);
        result.Should().NotBeNull();
    }
}
