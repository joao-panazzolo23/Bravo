using Bravo.Application.Importing.Exceptions;
using Bravo.Application.Logging;
using Bravo.Domain.Geography.Entities;
using Bravo.Domain.Geography.Repositories;
using Bravo.Domain.Importing.Enums;
using Bravo.Domain.Importing.Models;
using Bravo.Domain.Importing.Services;
using Microsoft.Extensions.Logging;

namespace Bravo.Application.Importing.Sources.Ibge;

/// <summary>
/// Imports IBGE states and then their municipalities.
/// </summary>
/// <remarks>
/// The stages are strictly ordered: a municipality references its state by IBGE code, so the
/// state upsert must be committed before the municipality stage starts. Both stages stream and
/// write in fixed-size batches, and a short write is treated as a permanent failure instead of a
/// silent partial import.
///
/// This type owns the orchestration; <see cref="IIbgeReader"/> owns acquisition.
/// </remarks>
public sealed class IbgeImportSource(
    IIbgeReader reader,
    IGeographyRepository geography,
    ILogger<IbgeImportSource> logger) : IImportSource
{
    public const string SourceKey = "IBGE";

    private const int OperationCount = 2;

    public string Key => SourceKey;

    public async Task ExecuteAsync(
        ImportExecutionContext context,
        CancellationToken cancellationToken)
    {
        context.Report(ImportStage.References, OperationCount, 0, 0, 0, 0, "IBGE states");

        var states = await ImportStatesAsync(context.BatchSize, cancellationToken);

        context.Report(
            ImportStage.References, OperationCount, 1, 0, states, 0, "IBGE municipalities");

        var municipalities = await ImportMunicipalitiesAsync(context.BatchSize, cancellationToken);

        context.Report(
            ImportStage.Persisting, OperationCount, 2, 0, states + municipalities, 0, null);

        PersistenceLog.IbgeImportCompleted(logger, states, municipalities);
    }

    private async Task<int> ImportStatesAsync(int batchSize, CancellationToken cancellationToken)
    {
        var buffer = new List<State>(batchSize);
        var imported = 0;
        var observed = 0;

        await foreach (var state in reader.StreamStatesAsync(cancellationToken))
        {
            observed++;

            if (state is null)
                continue;

            buffer.Add(state);

            if (buffer.Count < batchSize)
                continue;

            imported += await geography.UpsertStatesAsync(buffer.ToArray(), cancellationToken);
            buffer.Clear();
        }

        if (buffer.Count > 0)
            imported += await geography.UpsertStatesAsync(buffer.ToArray(), cancellationToken);

        if (observed == 0)
            throw new ImportPermanentException("IBGE returned no states.");

        return imported;
    }

    private async Task<int> ImportMunicipalitiesAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        var buffer = new List<MunicipalityUpsert>(batchSize);
        var imported = 0;

        await foreach (var municipality in reader.StreamMunicipalitiesAsync(cancellationToken))
        {
            buffer.Add(municipality);

            if (buffer.Count < batchSize)
                continue;

            imported += await PersistMunicipalitiesAsync(buffer, cancellationToken);
            buffer.Clear();
        }

        if (buffer.Count > 0)
            imported += await PersistMunicipalitiesAsync(buffer, cancellationToken);

        return imported;
    }

    /// <summary>
    /// Persists one municipality batch, treating a short write as a permanent failure.
    /// </summary>
    /// <remarks>
    /// The repository reports how many rows found their state. A short count means the state
    /// stage did not commit, and continuing would leave municipalities unresolvable, so the job
    /// fails instead of finishing with silently missing geography.
    /// </remarks>
    private async Task<int> PersistMunicipalitiesAsync(
        List<MunicipalityUpsert> buffer,
        CancellationToken cancellationToken)
    {
        var persisted = await geography.UpsertMunicipalitiesAsync(buffer.ToArray(), cancellationToken);

        if (persisted < buffer.Count)
        {
            throw new ImportPermanentException(
                $"Persisted {persisted} of {buffer.Count} IBGE municipalities; " +
                "some states are missing and must be imported first.");
        }

        return persisted;
    }
}
