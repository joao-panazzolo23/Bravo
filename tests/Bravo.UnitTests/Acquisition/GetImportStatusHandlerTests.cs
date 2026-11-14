using Bravo.Application.Acquisition.Queries;
using Bravo.Application.Acquisition.QueryHandlers;
using Bravo.Application.Acquisition.Repositories;
using Bravo.Domain.Acquisition.Entities;
using Bravo.Domain.Acquisition.Enums;
using FluentAssertions;
using Moq;

namespace Bravo.UnitTests.Acquisition;

public class GetImportStatusHandlerTests
{
    private readonly Mock<IImportExecutionRepository> _executionRepoMock = new();
    private readonly GetImportStatusHandler _handler;

    public GetImportStatusHandlerTests()
    {
        _handler = new GetImportStatusHandler(_executionRepoMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenExecutionNotFound()
    {
        var id = Guid.NewGuid();
        _executionRepoMock
            .Setup(x => x.GetById(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ImportExecution?)null);

        var result = await _handler.Handle(new GetImportStatusQuery(id), CancellationToken.None);

        result.StatusCode.Should().Be(404);
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnStatusViewModel_WhenExecutionFound()
    {
        var execution = ImportExecution.Create(ImportOperationType.IbgeGeography, "2024");
        execution.MarkRunning();
        execution.UpdateProgress("Municipalities", 5570, 5500);

        _executionRepoMock
            .Setup(x => x.GetById(execution.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(execution);

        var result = await _handler.Handle(new GetImportStatusQuery(execution.Id), CancellationToken.None);

        result.StatusCode.Should().Be(200);
        result.Data.Should().NotBeNull();
        result.Data!.Id.Should().Be(execution.Id);
        result.Data.OperationType.Should().Be("IbgeGeography");
        result.Data.Scope.Should().Be("2024");
        result.Data.Status.Should().Be("Running");
        result.Data.CurrentStage.Should().Be("Municipalities");
        result.Data.RecordsProcessed.Should().Be(5570);
        result.Data.RecordsPersisted.Should().Be(5500);
    }
}

