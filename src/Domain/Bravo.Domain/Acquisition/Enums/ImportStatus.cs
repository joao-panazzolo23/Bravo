namespace Bravo.Domain.Acquisition.Enums;

public enum ImportStatus
{
    Pending,
    Running,
    Completed,
    CompletedWithErrors,
    Failed,
    CancellationRequested,
    Cancelled
}

