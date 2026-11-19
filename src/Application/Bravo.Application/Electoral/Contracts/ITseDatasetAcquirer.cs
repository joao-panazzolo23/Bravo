using Bravo.Application.Electoral.Models;
using Bravo.Application.Importing;

namespace Bravo.Application.Electoral.Contracts;

public sealed record AcquiredDataset(
    string FilePath,
    string FileName,
    long SizeBytes,
    string Sha256,
    string SourceUrl,
    string LayoutVersion);

public interface ITseDatasetAcquirer
{
    /// <summary>
    /// Downloads the candidate archive for <paramref name="year"/> and extracts every state
    /// entry it contains. The state list is discovered from the archive, never hardcoded.
    /// </summary>
    Task<IReadOnlyList<AcquiredCandidateFile>> AcquireCandidatesAsync(
        int year,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Downloads and extracts the section-results archive for a single (year, state).
    /// </summary>
    Task<AcquiredDataset> AcquireResultsAsync(
        int year,
        string state,
        CancellationToken cancellationToken = default
    );
}

public sealed record AcquiredCandidateFile(
    string StateAcronym,
    AcquiredDataset Dataset);
