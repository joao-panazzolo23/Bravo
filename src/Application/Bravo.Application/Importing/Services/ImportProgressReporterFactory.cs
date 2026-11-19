using Bravo.Domain.Importing.Services;
using Bravo.Domain.Importing.Entities;
using Microsoft.Extensions.Logging;
using Bravo.Domain.Importing.Repositories;

namespace Bravo.Application.Importing.Services;

/// <summary>
/// Creates a progress reporter bound to one work unit, persisting through the repository.
/// </summary>
public sealed class ImportProgressReporterFactory(
    IImportWorkUnitRepository workUnits,
    ILoggerFactory loggerFactory)
{
    public IImportProgressReporter Create(ImportJob job, ImportWorkUnit workUnit)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(workUnit);

        var logger = loggerFactory.CreateLogger<ImportProgressReporter>();

        return new ImportProgressReporter(
            (snapshot, cancellationToken) => PersistAsync(workUnit, snapshot, cancellationToken),
            logger);
    }

    private Task PersistAsync(
        ImportWorkUnit workUnit,
        ImportProgressSnapshot snapshot,
        CancellationToken cancellationToken)
        => workUnits.ReportProgressAsync(
            workUnit.Id,
            workUnit.FencingToken,
            snapshot.Stage,
            snapshot.ProcessedRecords,
            snapshot.InsertedRecords,
            snapshot.UpdatedRecords,
            snapshot.RejectedRecords,
            snapshot.TotalRecords,
            cancellationToken);
}
