using Bravo.Application.Acquisition.Commands;
using Bravo.Application.Acquisition.Handlers;
using Bravo.Application.Acquisition.Repositories;
using Bravo.Domain.Acquisition.Entities;
using Bravo.Domain.Acquisition.Enums;
using Bravo.Shared.Data;
using FluentAssertions;
using Moq;

namespace Bravo.UnitTests.Acquisition;

public class CancelImportHandlerTests
{
    private readonly Mock<IImportExecutionRepository> _executionRepoMock = new();
    private readonly Mock<IUnityOfWork> _unityOfWorkMock = new();
    private readonly CancelImportHandler _handler;

    public CancelImportHandlerTests()
    {
        _handler = new CancelImportHandler(
            _executionRepoMock.Object,
            _unityOfWorkMock.Object
        );
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenExecutionDoesNotExist()
    {
        var id = Guid.NewGuid();
        _executionRepoMock
            .Setup(x => x.GetById(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ImportExecution?)null);

        var result = await _handler.Handle(new CancelImportCommand(id), CancellationToken.None);

        result.StatusCode.Should().Be(404);
        _unityOfWorkMock.Verify(x => x.Commit(), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnBadRequest_WhenExecutionAlreadyFinished()
    {
        var execution = ImportExecution.Create(ImportOperationType.IbgeGeography);
        execution.MarkRunning();
        execution.MarkCompleted();

        _executionRepoMock
            .Setup(x => x.GetById(execution.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(execution);

        var result = await _handler.Handle(new CancelImportCommand(execution.Id), CancellationToken.None);

        result.StatusCode.Should().Be(400);
        _unityOfWorkMock.Verify(x => x.Commit(), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRequestCancellation_WhenExecutionIsActive()
    {
        var execution = ImportExecution.Create(ImportOperationType.IbgeGeography);
        execution.MarkRunning();

        _executionRepoMock
            .Setup(x => x.GetById(execution.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(execution);

        var result = await _handler.Handle(new CancelImportCommand(execution.Id), CancellationToken.None);

        result.StatusCode.Should().Be(200);
        execution.Status.Should().Be(ImportStatus.CancellationRequested);
        _executionRepoMock.Verify(x => x.Update(execution, It.IsAny<CancellationToken>()), Times.Once);
        _unityOfWorkMock.Verify(x => x.Commit(), Times.Once);
    }
}

