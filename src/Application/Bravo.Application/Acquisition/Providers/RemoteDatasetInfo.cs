namespace Bravo.Application.Acquisition.Providers;

/// <summary>
/// A parsed record and the reference used to report parsing failures.
/// A null Record means the source row could not be parsed and must be rejected.
/// </summary>
public record ParsedRecord<T>(T? Record, string SourceReference);

/// <summary>
/// Result of a batch persistence operation.
/// </summary>
public record PersistenceResult(int Inserted, int Updated, int Skipped)
{
    public static PersistenceResult Empty { get; } = new(0, 0, 0);
}

/// <summary>
/// Conditional request headers derived from a previous successful acquisition.
/// </summary>
public record ConditionalRequest(string? ETag, DateTimeOffset? LastModified);
