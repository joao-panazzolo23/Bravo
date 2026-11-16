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
    /// Downloads the candidate archive for the scope and returns the extracted CSV for the
    /// requested state.
    /// </summary>
    Task<AcquiredDataset> AcquireCandidatesAsync(
        TseImportScope scope,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads the section-results archive for the scope and returns the extracted CSV for
    /// the requested state.
    /// </summary>
    Task<AcquiredDataset> AcquireResultsAsync(
        TseImportScope scope,
        CancellationToken cancellationToken = default);
}
