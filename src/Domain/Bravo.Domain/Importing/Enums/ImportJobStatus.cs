namespace Bravo.Domain.Importing.Enums;

public enum ImportJobStatus
{
    Pending,
    Running,
    Completed,
    CompletedWithErrors,
    Failed,
    Cancelled
}
