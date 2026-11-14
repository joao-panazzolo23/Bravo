namespace Bravo.Application.Acquisition.Services;

public interface IImportExecutionQueue
{
    ValueTask Enqueue(Guid executionId, CancellationToken ct = default);
}
