using Bravo.Application.Imports.Common.Exceptions;
using Bravo.Application.Imports.Ibge;
using Bravo.Application.Imports.Ibge.Abstractions;
using Bravo.Application.Imports.Ibge.Models;
using Bravo.Application.Imports.Ibge.Services;
using Bravo.Domain.Geography.Entities;
using Bravo.Domain.Geography.Repositories;
using Bravo.Domain.Importing.Enums;
using Bravo.Domain.Importing.Models;
using Bravo.UnitTests.Logging;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bravo.UnitTests.Importing;

/// <summary>
/// The IBGE orchestration moved from infrastructure to the application layer. These tests pin
/// the behaviour that move must preserve: states first, municipalities batched, and a short
/// write treated as a permanent failure rather than a silent partial import.
/// </summary>
public sealed class IbgeImportSourceTests
{
    private sealed class RecordingProgress : IProgress<ImportExecutionContext.OperationProgress>
    {
        public List<ImportExecutionContext.OperationProgress> Reports { get; } = [];

        public void Report(ImportExecutionContext.OperationProgress value) => Reports.Add(value);
    }

    private sealed class FakeReader(
        IReadOnlyList<IbgeStateRecord> states,
        IReadOnlyList<IbgeMunicipalityRecord> municipalities) : IIbgeReader
    {
        public async IAsyncEnumerable<IbgeStateRecord> StreamStatesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var state in states)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return state;
            }
        }

        public async IAsyncEnumerable<IbgeMunicipalityRecord> StreamMunicipalitiesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var municipality in municipalities)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return municipality;
            }
        }
    }

    private sealed class RecordingGeography : IGeographyRepository
    {
        public List<IReadOnlyCollection<State>> StateBatches { get; } = [];
        public List<IReadOnlyCollection<MunicipalityUpsert>> MunicipalityBatches { get; } = [];
        public Func<int, int> MunicipalityResult { get; set; } = input => input;

        public Task<IReadOnlyDictionary<string, Guid>> GetStateIdsByAcronymAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, Guid>>(new Dictionary<string, Guid>());

        public Task<IReadOnlyDictionary<int, Guid>> GetMunicipalityIdsByIbgeCodeAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<int, Guid>>(new Dictionary<int, Guid>());

        public Task<IReadOnlyList<MunicipalityLookup>> GetMunicipalitiesByStateAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MunicipalityLookup>>([]);

        public Task<IReadOnlyList<MunicipalityLookup>> GetMunicipalitiesByStateAsync(
            Guid stateId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MunicipalityLookup>>([]);

        public Task<int> UpsertStatesAsync(
            IReadOnlyCollection<State> states,
            CancellationToken cancellationToken = default)
        {
            StateBatches.Add(states);
            return Task.FromResult(states.Count);
        }

        public Task<int> UpsertMunicipalitiesAsync(
            IReadOnlyCollection<MunicipalityUpsert> municipalities,
            CancellationToken cancellationToken = default)
        {
            MunicipalityBatches.Add(municipalities);
            return Task.FromResult(MunicipalityResult(municipalities.Count));
        }
    }

    private static ImportExecutionContext Context(int batchSize, RecordingProgress progress)
        => new(Guid.CreateVersion7(), batchSize, 1, progress);

    /// <summary>
    /// Builds the facade around the importer, which is the unit that owns the orchestration.
    /// </summary>
    private static IbgeImportSource CreateSource(IIbgeReader reader, RecordingGeography geography)
        => new(new IbgeReferenceImporter(
            reader, geography, NullLogger<IbgeReferenceImporter>.Instance));

    private static IbgeStateRecord SampleState(int code = 11)
        => new(code, $"S{code}", $"STATE {code}", "NORTE");

    [Fact]
    public async Task StatesAreWrittenBeforeMunicipalitiesAndInBatches()
    {
        var geography = new RecordingGeography();
        var reader = new FakeReader(
            [SampleState(), SampleState(12), SampleState(13)],
            [new IbgeMunicipalityRecord(1, "A", 11), new IbgeMunicipalityRecord(2, "B", 11)]);

        var source = CreateSource(reader, geography);
        var progress = new RecordingProgress();

        await source.ExecuteAsync(Context(batchSize: 2, progress), CancellationToken.None);

        geography.StateBatches.Should().HaveCount(2);
        geography.StateBatches.Sum(batch => batch.Count).Should().Be(3);
        geography.MunicipalityBatches.Should().ContainSingle();
        geography.MunicipalityBatches[0].Should().HaveCount(2);

        progress.Reports.Should().NotBeEmpty();
        progress.Reports.Last().Stage.Should().Be(ImportStage.Persisting);
        progress.Reports.Last().ProcessedRecords.Should().Be(5);
    }

    [Fact]
    public async Task UnusableStatesAreRejectedButStillCountAsObserved()
    {
        var geography = new RecordingGeography();
        var reader = new FakeReader([new IbgeStateRecord(-1, null, "INVALID", null), SampleState()], []);

        var source = CreateSource(reader, geography);

        await source.ExecuteAsync(Context(10, new RecordingProgress()), CancellationToken.None);

        geography.StateBatches.Should().ContainSingle();
        geography.StateBatches[0].Should().HaveCount(1);
    }

    [Fact]
    public async Task AnEmptyStateStreamFailsPermanently()
    {
        var geography = new RecordingGeography();
        var reader = new FakeReader([], []);

        var source = CreateSource(reader, geography);

        var action = async () => await source.ExecuteAsync(
            Context(10, new RecordingProgress()), CancellationToken.None);

        await action.Should().ThrowAsync<ImportPermanentException>();
    }

    [Fact]
    public async Task AShortMunicipalityWriteFailsPermanently()
    {
        var geography = new RecordingGeography
        {
            // Half the rows found no state, which is exactly the condition that must abort.
            MunicipalityResult = input => input / 2
        };

        var reader = new FakeReader(
            [SampleState()],
            [new IbgeMunicipalityRecord(1, "A", 11), new IbgeMunicipalityRecord(2, "B", 11)]);

        var source = CreateSource(reader, geography);

        var action = async () => await source.ExecuteAsync(
            Context(10, new RecordingProgress()), CancellationToken.None);

        await action.Should().ThrowAsync<ImportPermanentException>()
            .WithMessage("*some states are missing*");
    }

    [Fact]
    public void TheSourceKeepsItsRegistrationKey()
    {
        var source = CreateSource(new FakeReader([], []), new RecordingGeography());

        source.Key.Should().Be("IBGE");
    }

    [Fact]
    public async Task CancellationPropagatesWhileStreaming()
    {
        var geography = new RecordingGeography();
        var reader = new FakeReader([SampleState()], []);
        var source = CreateSource(reader, geography);

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var action = async () => await source.ExecuteAsync(
            Context(10, new RecordingProgress()), cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }
}
