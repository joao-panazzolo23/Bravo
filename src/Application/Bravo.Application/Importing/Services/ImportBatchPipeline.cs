using Bravo.Domain.Importing.Services;
using Bravo.Application.Importing.Exceptions;
using Bravo.Domain.Importing.Models;
using Bravo.Domain.Importing.Entities;
using Microsoft.Extensions.Logging;

namespace Bravo.Application.Importing.Services;

public sealed class ImportBatchPipeline<TRecord>(
    string unitKey,
    IImportSource<TRecord> source,
    IImportRecordValidator<TRecord> validator,
    IImportBatchStore<TRecord> store,
    ILogger<ImportBatchPipeline<TRecord>> logger) : IImportPipeline
{
    public string UnitKey { get; } = unitKey;

    public async Task ProcessAsync(
        ImportJob job,
        ImportWorkUnit workUnit,
        CancellationToken cancellationToken = default)
    {
        var cursor = workUnit.Cursor;

        while (true)
        {
            var batch = await source.ReadBatchAsync(cursor, job.BatchSize, cancellationToken);

            if (batch.HasMore && string.Equals(batch.NextCursor, cursor, StringComparison.Ordinal))
                throw new ImportPermanentException($"The import source for '{UnitKey}' did not advance its cursor.");

            var validation = Validate(batch, cursor);
            var context = new ImportBatchWriteContext(
                job.Id,
                workUnit.Id,
                workUnit.FencingToken,
                workUnit.LeaseOwner ?? throw new ImportLeaseLostException("The work unit has no active lease owner."),
                batch.NextCursor,
                !batch.HasMore,
                job.LeaseDurationSeconds);

            var startedAt = DateTime.UtcNow;
            await store.PersistBatchAsync(context, validation.Accepted, validation.Rejected, cancellationToken);

            workUnit.AdvanceCursor(batch.NextCursor, validation.Accepted.Count, validation.Rejected.Count);

            logger.LogInformation(
                "Committed import batch for job {JobId} and work unit {WorkUnitId}: {Accepted} accepted, {Rejected} rejected, {ElapsedMs} ms.",
                job.Id,
                workUnit.Id,
                validation.Accepted.Count,
                validation.Rejected.Count,
                (DateTime.UtcNow - startedAt).TotalMilliseconds);

            if (!batch.HasMore)
                return;

            cursor = batch.NextCursor;
        }
    }

    private (List<TRecord> Accepted, List<ImportRejectedRecord> Rejected) Validate(
        ImportSourceBatch<TRecord> batch,
        string? cursor)
    {
        var accepted = new List<TRecord>(batch.Records.Count);
        var rejected = new List<ImportRejectedRecord>(batch.RejectedRecords.Count + batch.Records.Count);

        rejected.AddRange(batch.RejectedRecords);

        for (var index = 0; index < batch.Records.Count; index++)
        {
            var result = validator.Validate(batch.Records[index]);

            if (result.IsValid)
            {
                accepted.Add(batch.Records[index]);
                continue;
            }

            var sourceIdentifier = $"{cursor ?? "start"}:{index}";
            rejected.Add(new ImportRejectedRecord(
                sourceIdentifier,
                null,
                result.Category,
                result.Error ?? "Record validation failed."));
        }

        return (accepted, rejected);
    }
}
