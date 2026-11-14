using Bravo.Domain.Acquisition.Entities;
using Bravo.Domain.Acquisition.Enums;
using FluentAssertions;

namespace Bravo.UnitTests.Acquisition;

public class ImportExecutionStateTests
{
    [Fact]
    public void Create_ShouldInitializeWithPendingStatus()
    {
        var execution = ImportExecution.Create(ImportOperationType.IbgeGeography, "2024");

        execution.Status.Should().Be(ImportStatus.Pending);
        execution.OperationType.Should().Be(ImportOperationType.IbgeGeography);
        execution.Scope.Should().Be("2024");
        execution.StartedAt.Should().BeNull();
        execution.CompletedAt.Should().BeNull();
        execution.RecordsProcessed.Should().Be(0);
        execution.RecordsPersisted.Should().Be(0);
    }

    [Fact]
    public void MarkRunning_ShouldUpdateStatusAndStartedAt()
    {
        var execution = ImportExecution.Create(ImportOperationType.IbgeGeography);

        execution.MarkRunning();

        execution.Status.Should().Be(ImportStatus.Running);
        execution.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkCompleted_ShouldUpdateStatusAndCompletedAt()
    {
        var execution = ImportExecution.Create(ImportOperationType.IbgeGeography);
        execution.MarkRunning();

        execution.MarkCompleted();

        execution.Status.Should().Be(ImportStatus.Completed);
        execution.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkFailed_ShouldRecordFailureDetailsAndStatus()
    {
        var execution = ImportExecution.Create(ImportOperationType.IbgeGeography);
        execution.MarkRunning();

        execution.MarkFailed("Network timeout occurred");

        execution.Status.Should().Be(ImportStatus.Failed);
        execution.FailureDetails.Should().Be("Network timeout occurred");
        execution.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void RequestCancellation_ShouldSetCancellationRequested()
    {
        var execution = ImportExecution.Create(ImportOperationType.IbgeGeography);
        execution.MarkRunning();

        execution.RequestCancellation();

        execution.Status.Should().Be(ImportStatus.CancellationRequested);
    }

    [Fact]
    public void MarkCancelled_ShouldSetCancelledAndReason()
    {
        var execution = ImportExecution.Create(ImportOperationType.IbgeGeography);
        execution.MarkRunning();

        execution.MarkCancelled("User requested abort");

        execution.Status.Should().Be(ImportStatus.Cancelled);
        execution.CancellationReason.Should().Be("User requested abort");
        execution.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateProgress_ShouldUpdateStageAndCounts()
    {
        var execution = ImportExecution.Create(ImportOperationType.IbgeGeography);

        execution.UpdateProgress("Municipalities", 1000, 950);

        execution.CurrentStage.Should().Be("Municipalities");
        execution.RecordsProcessed.Should().Be(1000);
        execution.RecordsPersisted.Should().Be(950);
    }
}

