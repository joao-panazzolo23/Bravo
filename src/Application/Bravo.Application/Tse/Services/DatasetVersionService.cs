using Bravo.Domain.Electoral.Services;
using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Enums;
using Bravo.Domain.Importing.Repositories;

namespace Bravo.Application.Imports.Tse.Services;

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

        // The version is created already in Importing. Registering it as Pending and updating it
        // in the next statement produced a second write per result scope without any observer
        // ever seeing the intermediate status.
        var version = await versions.CreateAsync(
            DatasetVersion.Create(record.Id, scope, jobId, DatasetVersionStatus.Importing),
            cancellationToken
        );

        return version;
    }

    public Task<bool> CompleteAsync(Guid versionId, CancellationToken cancellationToken = default)
        => versions.ActivateAsync(versionId, cancellationToken);

    public Task<bool> FailAsync(Guid versionId, CancellationToken cancellationToken = default)
        => versions.SetStatusAsync(versionId, DatasetVersionStatus.Failed, cancellationToken);
}
