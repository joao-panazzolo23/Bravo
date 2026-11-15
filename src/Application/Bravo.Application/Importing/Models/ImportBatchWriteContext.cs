namespace Bravo.Application.Importing.Models;

public sealed record ImportBatchWriteContext(
    Guid JobId,
    Guid WorkUnitId,
    long FencingToken,
    string WorkerId,
    string? NextCursor,
    bool IsFinalBatch,
    int LeaseDurationSeconds
);
