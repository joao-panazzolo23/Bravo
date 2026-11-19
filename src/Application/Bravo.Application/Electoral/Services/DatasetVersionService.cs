using Bravo.Domain.Electoral.Services;
using Bravo.Domain.Importing.Services;
using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Enums;
using Bravo.Domain.Importing.Repositories;

namespace Bravo.Application.Electoral.Services;

/// <summary>
/// Registers the acquired file and creates (or reuses) a dataset version for a scope.
/// </summary>
/// <remarks>
/// The version is what results reference, so activating a replacement version supersedes the
/// previous one rather than duplicating votes.
/// </remarks>
public sealed class DatasetVersionService(
    IImportDatasetRepository datasets,
    IDatasetVersionRepository versions)
{
    public async Task<DatasetVersion> RegisterAsync(
        string provider,
        string dataset,
        int? electionYear,
        AcquiredDataset acquired,
        string scope,
        Guid jobId,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(acquired);

        var record = await datasets.RegisterAsync(
            ImportDataset.Create(
                provider,
                dataset,
                electionYear,
                acquired.LayoutVersion,
                acquired.SourceUrl,
                acquired.FileName,
                acquired.Sha256,
                acquired.SizeBytes,
                DateTime.UtcNow),
            cancellationToken
        );

        var version = await versions.CreateAsync(
            DatasetVersion.Create(record.Id, scope, jobId),
            cancellationToken
        );

        await versions.SetStatusAsync(version.Id, DatasetVersionStatus.Importing, cancellationToken);

        return version;
    }

    public Task<bool> CompleteAsync(Guid versionId, CancellationToken cancellationToken = default)
        => versions.ActivateAsync(versionId, cancellationToken);

    public Task<bool> FailAsync(Guid versionId, CancellationToken cancellationToken = default)
        => versions.SetStatusAsync(versionId, DatasetVersionStatus.Failed, cancellationToken);
}
