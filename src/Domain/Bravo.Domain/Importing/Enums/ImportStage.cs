namespace Bravo.Domain.Importing.Enums;

/// <summary>
/// Observable stages an import moves through. Persisted as text on the job row, so adding a
/// stage does not require a data migration of numeric values.
/// </summary>
/// <remarks>
/// Only the stages a source actually reports are listed. The previous enum carried values for
/// downloading, extracting, reading, parsing, validating, normalising and finalising that no
/// source ever emitted; they were a leftover from the work-unit model and made the progress
/// vocabulary look richer than the implementation. Adding one back is a one-line change when a
/// source really starts reporting it.
///
/// <see cref="Pending"/> and <see cref="Failed"/> remain because the job row is created and
/// settled with them, even though they are not progress reports.
/// </remarks>
public enum ImportStage
{
    Pending,
    Initializing,
    References,
    Candidates,
    Results,
    Persisting,
    Completed,
    Failed
}
