using Bravo.Application.Imports.Common.Exceptions;
using Bravo.Application.Imports.Ibge.Abstractions;
using Bravo.Application.Imports.Ibge.Models;
using Bravo.Application.Logging;
using Bravo.Domain.Geography.Entities;
using Bravo.Domain.Geography.Repositories;
using Bravo.Domain.Importing.Enums;
using Bravo.Domain.Importing.Models;
using Microsoft.Extensions.Logging;

namespace Bravo.Application.Imports.Ibge.Services;

/// <summary>
/// Imports IBGE states and then their municipalities.
/// </summary>
/// <remarks>
/// The stages are strictly ordered: a municipality references its state by IBGE code, so the state
/// upsert must be committed before the municipality stage starts. Both stages stream and write in
/// fixed-size batches, and a short write is treated as a permanent failure instead of a silent
/// partial import.
///
/// Validation lives here rather than in the reader. The reader reports exactly what IBGE published,
/// with nullable fields; this class decides whether a record can become a domain entity and records
/// a diagnostic when it cannot. A record that cannot be used is counted as rejected, never silently
/// dropped.
/// </remarks>
public sealed class IbgeReferenceImporter(
    IIbgeReader reader,
    IGeographyRepository geography,
    ILogger<IbgeReferenceImporter> logger)
{
    private const int OperationCount = 2;

    public async Task<IbgeImportOutcome> RunAsync(
        ImportExecutionContext context,
        CancellationToken cancellationToken)
    {
        context.Report(ImportStage.References, OperationCount, 0, 0, 0, 0, "IBGE states");

        var states = await ImportStatesAsync(context.BatchSize, cancellationToken);

        context.Report(
            ImportStage.References, OperationCount, 1, 0, states.Imported, states.Rejected,
            "IBGE municipalities");

        var municipalities = await ImportMunicipalitiesAsync(context.BatchSize, cancellationToken);

        context.Report(
            ImportStage.Persisting, OperationCount, 2, 0,
            states.Imported + municipalities.Imported,
            states.Rejected + municipalities.Rejected,
            null);

        PersistenceLog.IbgeImportCompleted(logger, states.Imported, municipalities.Imported);

        return new IbgeImportOutcome(
            states.Imported,
            municipalities.Imported,
            states.Rejected + municipalities.Rejected);
    }

    private async Task<StageOutcome> ImportStatesAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        var buffer = new List<State>(batchSize);
        var imported = 0;
        var observed = 0;
        var rejected = 0;

        await foreach (var record in reader.StreamStatesAsync(cancellationToken))
        {
            observed++;

            var state = Map(record, ref rejected);

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

        return new StageOutcome(imported, rejected);
    }

    /// <summary>
    /// Turns one published record into a state, or reports why it was discarded.
    /// </summary>
    /// <remarks>
    /// The fields are required by the import, not by the wire format: without an abbreviation the
    /// row cannot be matched by TSE scopes, and without a region it cannot be grouped. A record
    /// missing either is rejected with a diagnostic instead of failing the whole import.
    /// </remarks>
    private State? Map(IbgeStateRecord record, ref int rejected)
    {
        if (string.IsNullOrWhiteSpace(record.Abbreviation) || string.IsNullOrWhiteSpace(record.Region))
        {
            ValidationLog.IbgeRecordDiscarded(
                logger, "state", record.IbgeCode, "required fields are missing");

            rejected++;

            return null;
        }

        return State.Create(
            record.IbgeCode,
            record.Abbreviation.Trim(),
            record.Name ?? string.Empty,
            record.Region);
    }

    private async Task<StageOutcome> ImportMunicipalitiesAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        var buffer = new List<MunicipalityUpsert>(batchSize);
        var imported = 0;
        var rejected = 0;

        await foreach (var record in reader.StreamMunicipalitiesAsync(cancellationToken))
        {
            if (record.StateIbgeCode is null || string.IsNullOrWhiteSpace(record.Name))
            {
                ValidationLog.IbgeRecordDiscarded(
                    logger, "municipality", record.IbgeCode, "state could not be resolved");

                rejected++;

                continue;
            }

            buffer.Add(new MunicipalityUpsert(record.IbgeCode, record.Name, record.StateIbgeCode.Value));

            if (buffer.Count < batchSize)
                continue;

            imported += await PersistMunicipalitiesAsync(buffer, cancellationToken);
            buffer.Clear();
        }

        if (buffer.Count > 0)
            imported += await PersistMunicipalitiesAsync(buffer, cancellationToken);

        return new StageOutcome(imported, rejected);
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

    private readonly record struct StageOutcome(int Imported, int Rejected);
}

/// <summary>
/// What the IBGE import produced: persisted rows and rows the source published but could not be
/// used.
/// </summary>
public readonly record struct IbgeImportOutcome(int States, int Municipalities, int Rejected);
