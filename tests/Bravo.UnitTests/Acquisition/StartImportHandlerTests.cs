using Bravo.Application.Acquisition.Commands;
using Bravo.Application.Acquisition.Handlers;
using Bravo.Application.Acquisition.Repositories;
using Bravo.Application.Acquisition.Services;
using Bravo.Domain.Acquisition.Entities;
using Bravo.Domain.Acquisition.Enums;
using Bravo.Shared.Data;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Bravo.UnitTests.Acquisition;

public class StartImportHandlerTests
{
    private readonly Mock<IImportExecutionRepository> _executionRepoMock = new();
    private readonly Mock<IImportExecutionQueue> _queueMock = new();
    private readonly Mock<IUnityOfWork> _unityOfWorkMock = new();
    private readonly StartImportHandler _handler;

    public StartImportHandlerTests()
    {
        _handler = new StartImportHandler(
            _executionRepoMock.Object,
            _queueMock.Object,
            _unityOfWorkMock.Object,
            NullLogger<StartImportHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenImportAlreadyRunning()
    {
        var existing = ImportExecution.Create(ImportOperationType.TseResults, "2022:1");
        existing.MarkRunning();

        _executionRepoMock
            .Setup(x => x.GetRunningByType(ImportOperationType.TseResults, "2022:1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var command = new StartImportCommand(ImportOperationType.TseResults, "2022:1");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.StatusCode.Should().Be(409);
        _queueMock.Verify(x => x.Enqueue(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unityOfWorkMock.Verify(x => x.Commit(), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldCreateAndEnqueue_WhenNoImportRunning()
    {
        _executionRepoMock
            .Setup(x => x.GetRunningByType(ImportOperationType.IbgeGeography, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ImportExecution?)null);

        var command = new StartImportCommand(ImportOperationType.IbgeGeography);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.StatusCode.Should().Be(201);
        result.Data.Should().NotBeNull();
        result.Data!.Status.Should().Be(ImportStatus.Pending.ToString());

        _executionRepoMock.Verify(x => x.Add(It.IsAny<ImportExecution>(), It.IsAny<CancellationToken>()), Times.Once);
        _unityOfWorkMock.Verify(x => x.Commit(), Times.Once);
        _queueMock.Verify(x => x.Enqueue(result.Data.ExecutionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNormalizeScopeBeforePersisting()
    {
        _executionRepoMock
            .Setup(x => x.GetRunningByType(ImportOperationType.TseCandidates, "2022:1:SP", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ImportExecution?)null);

        var result = await _handler.Handle(
            new StartImportCommand(ImportOperationType.TseCandidates, " 2022 : 1 : sp "),
            CancellationToken.None);

        result.StatusCode.Should().Be(201);
        _executionRepoMock.Verify(
            x => x.Add(It.Is<ImportExecution>(e => e.Scope == "2022:1:SP"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldRejectMissingScopeForTseImport()
    {
        var result = await _handler.Handle(
            new StartImportCommand(ImportOperationType.TseResults),
            CancellationToken.None);

        result.StatusCode.Should().Be(400);
        _executionRepoMock.Verify(
            x => x.Add(It.IsAny<ImportExecution>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRejectScopeForGeographyImport()
    {
        var result = await _handler.Handle(
            new StartImportCommand(ImportOperationType.IbgeGeography, "2022"),
            CancellationToken.None);

        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Handle_ShouldRejectUnsupportedRound()
    {
        var result = await _handler.Handle(
            new StartImportCommand(ImportOperationType.TseResults, "2022:3"),
            CancellationToken.None);

        result.StatusCode.Should().Be(400);
    }
}
