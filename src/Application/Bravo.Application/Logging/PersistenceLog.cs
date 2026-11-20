using Microsoft.Extensions.Logging;

namespace Bravo.Application.Logging;

public static partial class PersistenceLog
{
    [LoggerMessage(
        EventId = 1401,
        Level = LogLevel.Debug,
        Message = "Persisting batch of {Count} records for '{EntityName}'.")]
    public static partial void BatchPersisting(
        ILogger logger,
        string entityName,
        int count);

    [LoggerMessage(
        EventId = 1402,
        Level = LogLevel.Debug,
        Message = "Persisted batch of {Count} records for '{EntityName}' in {ElapsedMs} ms.")]
    public static partial void BatchPersisted(
        ILogger logger,
        string entityName,
        int count,
        double elapsedMs);

    [LoggerMessage(
        EventId = 1403,
        Level = LogLevel.Error,
        Message = "Failed to persist batch for '{EntityName}'.")]
    public static partial void PersistenceFailed(
        ILogger logger,
        string entityName,
        Exception exception);

    [LoggerMessage(
        EventId = 1404,
        Level = LogLevel.Debug,
        Message = "Checkpoint persisted for job {JobId}, work unit {WorkUnitId}. Position: {Position}.")]
    public static partial void CheckpointPersisted(
        ILogger logger,
        Guid jobId,
        Guid workUnitId,
        string position);

    [LoggerMessage(
        EventId = 1405,
        Level = LogLevel.Warning,
        Message = "Could not persist progress for import job {JobId}.")]
    public static partial void ProgressPersistFailed(
        ILogger logger,
        Guid jobId,
        Exception exception);

    [LoggerMessage(
        EventId = 1406,
        Level = LogLevel.Error,
        Message = "Database integrity violation during persistence for '{EntityName}': {Details}.")]
    public static partial void IntegrityViolation(
        ILogger logger,
        string entityName,
        string details,
        Exception exception);

    [LoggerMessage(
        EventId = 1407,
        Level = LogLevel.Error,
        Message = "Transaction failed during persistence for '{EntityName}'.")]
    public static partial void TransactionFailed(
        ILogger logger,
        string entityName,
        Exception exception);

    [LoggerMessage(
        EventId = 1408,
        Level = LogLevel.Information,
        Message = "Imported {Year} TSE reference data: {Elections} election rounds, {Offices} offices, {Parties} parties, {Federations} federations.")]
    public static partial void ReferenceDataUpserted(
        ILogger logger,
        int year,
        int elections,
        int offices,
        int parties,
        int federations);

    [LoggerMessage(
        EventId = 1409,
        Level = LogLevel.Information,
        Message = "Imported {Count} TSE candidate registrations for {Year} across {States} state files.")]
    public static partial void CandidateRegistrationsUpserted(
        ILogger logger,
        int count,
        int year,
        int states);

    [LoggerMessage(
        EventId = 1410,
        Level = LogLevel.Information,
        Message = "Imported {Count} TSE section results for {Year}/{State}.")]
    public static partial void SectionResultsUpserted(
        ILogger logger,
        int count,
        int year,
        string state);

    [LoggerMessage(
        EventId = 1411,
        Level = LogLevel.Debug,
        Message = "Imported {Count} candidates for {Year}/{State}.")]
    public static partial void CandidatesUpsertedForState(
        ILogger logger,
        int count,
        int year,
        string state);

    [LoggerMessage(
        EventId = 1412,
        Level = LogLevel.Information,
        Message = "IBGE import finished with {States} states and {Municipalities} municipalities.")]
    public static partial void IbgeImportCompleted(
        ILogger logger,
        int states,
        int municipalities);
}
