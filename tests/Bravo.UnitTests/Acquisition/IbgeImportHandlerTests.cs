using Bravo.Application.Acquisition.Handlers;
using Bravo.Application.Acquisition.Providers;
using Bravo.Application.Acquisition.Repositories;
using Bravo.Application.Acquisition.Services;
using Bravo.Domain.Acquisition.Entities;
using Bravo.Domain.Acquisition.Enums;
using Bravo.Shared.Data;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Bravo.UnitTests.Acquisition;

public class IbgeImportHandlerTests
{
    private readonly Mock<IImportExecutionRepository> _executionRepoMock = new();
    private readonly Mock<IImportCheckpointRepository> _checkpointRepoMock = new();
    private readonly Mock<IDatasetProvenanceRepository> _provenanceRepoMock = new();
    private readonly Mock<IIbgeProvider> _ibgeProviderMock = new();
    private readonly Mock<IGeographyPersistenceService> _persistenceMock = new();
    private readonly Mock<IUnityOfWork> _unityOfWorkMock = new();
    private readonly IbgeImportHandler _handler;

    public IbgeImportHandlerTests()
    {
        _handler = new IbgeImportHandler(
            _executionRepoMock.Object,
            _checkpointRepoMock.Object,
            _provenanceRepoMock.Object,
            _ibgeProviderMock.Object,
            _persistenceMock.Object,
            _unityOfWorkMock.Object,
            NullLogger<IbgeImportHandler>.Instance
        );
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCompleteSuccessfully_WhenExecutionIsNew()
    {
        var execution = ImportExecution.Create(ImportOperationType.IbgeGeography);

        _executionRepoMock
            .Setup(x => x.GetById(execution.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(execution);

        _ibgeProviderMock
            .Setup(x => x.GetDatasetInfo(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IbgeDatasetInfo("states.json", "munis.json", "2024", "JSON"));

        var states = new List<IbgeStateRecord>
        {
            new(35, "SP", "São Paulo", "SE"),
            new(33, "RJ", "Rio de Janeiro", "SE")
        };

        var munis = new List<IbgeMunicipalityRecord>
        {
            new(3550308, "São Paulo", 35),
            new(3304557, "Rio de Janeiro", 33)
        };

        _ibgeProviderMock
            .Setup(x => x.StreamStates(It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(states));

        _ibgeProviderMock
            .Setup(x => x.StreamMunicipalities(It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(munis));

        _persistenceMock
            .Setup(x => x.UpsertStates(It.IsAny<IReadOnlyList<IbgeStateRecord>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        _persistenceMock
            .Setup(x => x.UpsertMunicipalities(It.IsAny<IReadOnlyList<IbgeMunicipalityRecord>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        await _handler.ExecuteAsync(execution.Id, CancellationToken.None);

        execution.Status.Should().Be(ImportStatus.Completed);
        execution.CurrentStage.Should().Be("MunicipalitiesCompleted");
        execution.RecordsPersisted.Should().Be(4);

        _provenanceRepoMock.Verify(x => x.Add(It.IsAny<DatasetProvenance>(), It.IsAny<CancellationToken>()), Times.Once);
        _checkpointRepoMock.Verify(x => x.Save(It.Is<ImportCheckpoint>(c => c.DatasetKey == "ibge-states"), It.IsAny<CancellationToken>()), Times.Once);
        _checkpointRepoMock.Verify(x => x.Save(It.Is<ImportCheckpoint>(c => c.DatasetKey == "ibge-municipalities"), It.IsAny<CancellationToken>()), Times.Once);
        _unityOfWorkMock.Verify(x => x.Commit(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSkipStates_WhenStatesAlreadyCheckpointed()
    {
        var execution = ImportExecution.Create(ImportOperationType.IbgeGeography);

        _executionRepoMock
            .Setup(x => x.GetById(execution.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(execution);

        _ibgeProviderMock
            .Setup(x => x.GetDatasetInfo(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IbgeDatasetInfo("states.json", "munis.json", "2024", "JSON"));

        var checkpoint = ImportCheckpoint.Create(execution.Id, "ibge-states", "{\"states\": 27}", 27);
        _checkpointRepoMock
            .Setup(x => x.GetLatest(execution.Id, "ibge-states", It.IsAny<CancellationToken>()))
            .ReturnsAsync(checkpoint);

        var munis = new List<IbgeMunicipalityRecord> { new(3550308, "São Paulo", 35) };
        _ibgeProviderMock
            .Setup(x => x.StreamMunicipalities(It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(munis));

        _persistenceMock
            .Setup(x => x.UpsertMunicipalities(It.IsAny<IReadOnlyList<IbgeMunicipalityRecord>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await _handler.ExecuteAsync(execution.Id, CancellationToken.None);

        _ibgeProviderMock.Verify(x => x.StreamStates(It.IsAny<CancellationToken>()), Times.Never);
        _persistenceMock.Verify(x => x.UpsertStates(It.IsAny<IReadOnlyList<IbgeStateRecord>>(), It.IsAny<CancellationToken>()), Times.Never);
        execution.Status.Should().Be(ImportStatus.Completed);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMarkCancelled_WhenTokenCancelled()
    {
        var execution = ImportExecution.Create(ImportOperationType.IbgeGeography);

        _executionRepoMock
            .Setup(x => x.GetById(execution.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(execution);

        _ibgeProviderMock
            .Setup(x => x.GetDatasetInfo(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IbgeDatasetInfo("states.json", "munis.json", "2024", "JSON"));

        _ibgeProviderMock
            .Setup(x => x.StreamStates(It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(new List<IbgeStateRecord>()));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await _handler.ExecuteAsync(execution.Id, cts.Token);

        execution.Status.Should().Be(ImportStatus.Cancelled);
        execution.CancellationReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMarkFailed_WhenExceptionThrown()
    {
        var execution = ImportExecution.Create(ImportOperationType.IbgeGeography);

        _executionRepoMock
            .Setup(x => x.GetById(execution.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(execution);

        _ibgeProviderMock
            .Setup(x => x.GetDatasetInfo(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        await _handler.ExecuteAsync(execution.Id, CancellationToken.None);

        execution.Status.Should().Be(ImportStatus.Failed);
        execution.FailureDetails.Should().Contain("Connection refused");
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }
}
