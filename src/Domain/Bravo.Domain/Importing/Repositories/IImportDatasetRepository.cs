using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Repositories;

namespace Bravo.Domain.Importing.Repositories;

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
