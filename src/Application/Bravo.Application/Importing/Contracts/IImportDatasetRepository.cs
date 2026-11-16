using Bravo.Domain.Importing.Entities;

namespace Bravo.Application.Importing.Contracts;

public interface IImportDatasetRepository
{
    Task<ImportDataset> RegisterAsync(
        ImportDataset dataset,
        CancellationToken cancellationToken = default);

    Task<ImportDataset?> FindByChecksumAsync(
        string provider,
        string dataset,
        int? electionYear,
        string checksum,
        CancellationToken cancellationToken = default);
}
