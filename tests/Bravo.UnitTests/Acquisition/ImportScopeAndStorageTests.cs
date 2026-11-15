using Bravo.Application.Acquisition.Options;
using Bravo.Application.Acquisition.Scopes;
using Bravo.Domain.Acquisition.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bravo.UnitTests.Acquisition;

public class ImportScopeTests
{
    [Fact]
    public void Parse_ShouldRejectScopeForGeography()
    {
        ImportScope.Parse(ImportOperationType.IbgeGeography, "2022").StatusCode.Should().Be(400);
        ImportScope.Parse(ImportOperationType.IbgeGeography, null).StatusCode.Should().Be(200);
    }

    [Fact]
    public void Parse_ShouldRequireYearForTse()
    {
        ImportScope.Parse(ImportOperationType.TseResults, null).StatusCode.Should().Be(400);
        ImportScope.Parse(ImportOperationType.TseResults, "  ").StatusCode.Should().Be(400);
    }

    [Theory]
    [InlineData("2022", "2022")]
    [InlineData("2022:1", "2022:1")]
    [InlineData("2022:2", "2022:2")]
    [InlineData(" 2022 : 1 : sp ", "2022:1:SP")]
    [InlineData("2018:1:BR", "2018:1:BR")]
    public void Parse_ShouldNormalizeValidScopes(string input, string expected)
    {
        var result = ImportScope.Parse(ImportOperationType.TseResults, input);

        result.StatusCode.Should().Be(200);
        result.Data!.Canonical.Should().Be(expected);
    }

    [Theory]
    [InlineData("2022:3")]
    [InlineData("2022:0")]
    [InlineData("2022:1:XX")]
    [InlineData("1850")]
    [InlineData("abc")]
    [InlineData("2022:1:SP:extra")]
    public void Parse_ShouldRejectInvalidScopes(string input)
    {
        ImportScope.Parse(ImportOperationType.TseResults, input).StatusCode.Should().Be(400);
    }
}

public class TemporaryFileStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bravo-store-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private Infrastructure.Acquisition.Storage.TemporaryFileStore CreateStore()
    {
        var options = Options.Create(new AcquisitionOptions { TempDirectory = _root });
        return new Infrastructure.Acquisition.Storage.TemporaryFileStore(
            options,
            NullLogger<Infrastructure.Acquisition.Storage.TemporaryFileStore>.Instance);
    }

    [Fact]
    public async Task FindVerified_ShouldReturnNullBeforeMetadataExists()
    {
        var store = CreateStore();
        var executionId = Guid.NewGuid();
        var path = store.CreatePath(executionId, "dataset", ".zip");
        await File.WriteAllBytesAsync(path, [1, 2, 3]);

        store.FindVerified(executionId, "dataset").Should().BeNull();
    }

    [Fact]
    public async Task FindVerified_ShouldReturnMetadataForIntactFile()
    {
        var store = CreateStore();
        var executionId = Guid.NewGuid();
        var path = store.CreatePath(executionId, "dataset", ".zip");
        await File.WriteAllBytesAsync(path, [1, 2, 3]);

        var metadata = new Application.Acquisition.Services.CachedDownload(
            path, 3, "abcd", DateTime.UtcNow, "\"etag\"", null);
        store.SaveMetadata(path, metadata);

        var found = store.FindVerified(executionId, "dataset");
        found.Should().NotBeNull();
        found!.Sha256.Should().Be("abcd");
        found.ETag.Should().Be("\"etag\"");
    }

    [Fact]
    public async Task FindVerified_ShouldIgnoreFileWhoseSizeChanged()
    {
        var store = CreateStore();
        var executionId = Guid.NewGuid();
        var path = store.CreatePath(executionId, "dataset", ".zip");
        await File.WriteAllBytesAsync(path, [1, 2, 3]);
        store.SaveMetadata(path, new Application.Acquisition.Services.CachedDownload(path, 3, "abcd", DateTime.UtcNow));

        await File.WriteAllBytesAsync(path, [1, 2, 3, 4, 5]);

        store.FindVerified(executionId, "dataset").Should().BeNull();
    }

    [Fact]
    public async Task TryDelete_ShouldRemoveFileAndSidecar()
    {
        var store = CreateStore();
        var executionId = Guid.NewGuid();
        var path = store.CreatePath(executionId, "dataset", ".zip");
        await File.WriteAllBytesAsync(path, [1]);
        store.SaveMetadata(path, new Application.Acquisition.Services.CachedDownload(path, 1, "x", DateTime.UtcNow));

        store.TryDelete(path);

        File.Exists(path).Should().BeFalse();
        File.Exists(path + ".meta.json").Should().BeFalse();
    }

    [Fact]
    public void CleanupOlderThan_ShouldRemoveExpiredFilesOnly()
    {
        var store = CreateStore();
        var path = store.CreatePath(Guid.NewGuid(), "old", ".zip");
        File.WriteAllBytes(path, [1]);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-10));

        var fresh = store.CreatePath(Guid.NewGuid(), "fresh", ".zip");
        File.WriteAllBytes(fresh, [1]);

        var removed = store.CleanupOlderThan(TimeSpan.FromDays(1));

        removed.Should().Be(1);
        File.Exists(path).Should().BeFalse();
        File.Exists(fresh).Should().BeTrue();
    }
}
