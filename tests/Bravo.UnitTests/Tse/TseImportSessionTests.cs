using Bravo.Application.Imports.Tse.Services;
using Bravo.Domain.Electoral.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bravo.UnitTests.Tse;

/// <summary>
/// Pins the job-scoped archive handoff that removed the duplicate candidate download.
/// </summary>
/// <remarks>
/// The behaviour that matters is not "a dictionary caches a value" but "the same year is acquired
/// once per job, and the directories are removed no matter how the job ends". Both are asserted
/// here without a database or a network.
/// </remarks>
public sealed class TseImportSessionTests
{
    private sealed class CountingAcquirer : ITseDatasetAcquirer
    {
        private readonly string _root;

        public CountingAcquirer(string root) => _root = root;

        public int CandidateCalls { get; private set; }

        public Task<IReadOnlyList<AcquiredCandidateFile>> AcquireCandidatesAsync(
            int year,
            CancellationToken cancellationToken = default)
        {
            CandidateCalls++;

            var directory = Path.Combine(_root, $"cand-{year}-{Guid.CreateVersion7():N}");
            Directory.CreateDirectory(directory);

            var file = Path.Combine(directory, $"consulta_cand_{year}_SP.csv");
            File.WriteAllText(file, "header\n");

            return Task.FromResult<IReadOnlyList<AcquiredCandidateFile>>([
                new AcquiredCandidateFile(
                    "SP",
                    new AcquiredDataset(file, Path.GetFileName(file), 1, "sha", "url", "v1"))
            ]);
        }

        public Task<AcquiredDataset> AcquireResultsAsync(
            int year,
            string state,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private static TseImportSession CreateSession()
        => new(NullLogger<TseImportSession>.Instance);

    [Fact]
    public async Task TheSameYearIsAcquiredOnlyOnce()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tse-session-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var acquirer = new CountingAcquirer(root);
            await using var session = CreateSession();

            var first = await session.GetOrAcquireCandidatesAsync(2022, acquirer, CancellationToken.None);
            var second = await session.GetOrAcquireCandidatesAsync(2022, acquirer, CancellationToken.None);

            acquirer.CandidateCalls.Should().Be(1, "the second stage must reuse the first download");
            second.Should().BeSameAs(first);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DifferentYearsAreAcquiredIndependently()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tse-session-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var acquirer = new CountingAcquirer(root);
            await using var session = CreateSession();

            await session.GetOrAcquireCandidatesAsync(2018, acquirer, CancellationToken.None);
            await session.GetOrAcquireCandidatesAsync(2022, acquirer, CancellationToken.None);

            acquirer.CandidateCalls.Should().Be(2);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DisposingTheSessionRemovesTrackedDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tse-session-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var acquirer = new CountingAcquirer(root);
            var session = CreateSession();

            var files = await session.GetOrAcquireCandidatesAsync(2022, acquirer, CancellationToken.None);
            var directory = Path.GetDirectoryName(files[0].Dataset.FilePath)!;

            Directory.Exists(directory).Should().BeTrue();

            await session.DisposeAsync();

            Directory.Exists(directory).Should().BeFalse("temporary files must not outlive the job");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CleanupIsIdempotent()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tse-session-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var acquirer = new CountingAcquirer(root);
            var session = CreateSession();

            await session.GetOrAcquireCandidatesAsync(2022, acquirer, CancellationToken.None);

            await session.DisposeAsync();

            // A second dispose must not throw: the executor disposes the scope after it may already
            // have disposed the session explicitly on a failure path.
            var act = async () => await session.DisposeAsync();

            await act.Should().NotThrowAsync();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AnAcquisitionFailureLeavesNothingCached()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tse-session-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var failing = new FailingAcquirer();
            await using var session = CreateSession();

            var act = async () => await session.GetOrAcquireCandidatesAsync(
                2022, failing, CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>();

            // Nothing was cached, so a retry within the same job would have to acquire again
            // rather than silently reuse a partial result.
            var acquirer = new CountingAcquirer(root);
            var files = await session.GetOrAcquireCandidatesAsync(2022, acquirer, CancellationToken.None);

            files.Should().ContainSingle();
            acquirer.CandidateCalls.Should().Be(1);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private sealed class FailingAcquirer : ITseDatasetAcquirer
    {
        public Task<IReadOnlyList<AcquiredCandidateFile>> AcquireCandidatesAsync(
            int year,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("simulated download failure");

        public Task<AcquiredDataset> AcquireResultsAsync(
            int year,
            string state,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
