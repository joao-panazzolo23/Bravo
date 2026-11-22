using Microsoft.Extensions.Logging;

namespace Bravo.Application.Logging;

public static partial class ValidationLog
{
    [LoggerMessage(
        EventId = 1301,
        Level = LogLevel.Warning,
        Message = "Record rejected in scope '{Scope}': {Reason}.")]
    public static partial void RecordRejected(
        ILogger logger,
        string scope,
        string reason);

    [LoggerMessage(
        EventId = 1302,
        Level = LogLevel.Warning,
        Message = "Unresolved reference in scope '{Scope}': {ReferenceType} '{ReferenceValue}'.")]
    public static partial void UnresolvedReference(
        ILogger logger,
        string scope,
        string referenceType,
        string referenceValue);

    [LoggerMessage(
        EventId = 1303,
        Level = LogLevel.Debug,
        Message = "Validation completed for scope '{Scope}'. Valid: {ValidCount}, Invalid: {InvalidCount}.")]
    public static partial void ValidationCompleted(
        ILogger logger,
        string scope,
        int validCount,
        int invalidCount);

    [LoggerMessage(
        EventId = 1304,
        Level = LogLevel.Information,
        Message = "Created synthetic geographic scope '{Scope}'.")]
    public static partial void SyntheticScopeCreated(
        ILogger logger,
        string scope);

    [LoggerMessage(
        EventId = 1305,
        Level = LogLevel.Warning,
        Message = "File '{FilePath}' is missing optional columns for layout '{LayoutVersion}': {MissingColumns}.")]
    public static partial void MissingColumnsDetected(
        ILogger logger,
        string filePath,
        string layoutVersion,
        string missingColumns);

    [LoggerMessage(
        EventId = 1306,
        Level = LogLevel.Warning,
        Message = "Skipping TSE {EntityType} for {Year}/{State}: no geography row could be resolved.")]
    public static partial void GeographyResolutionSkipped(
        ILogger logger,
        string entityType,
        int year,
        string state);

    [LoggerMessage(
        EventId = 1307,
        Level = LogLevel.Warning,
        Message = "Discarding IBGE {EntityType} {Id}: {Reason}.")]
    public static partial void IbgeRecordDiscarded(
        ILogger logger,
        string entityType,
        int id,
        string reason);

}

///TODO: Remove. Is completelly expected to have multiple CandidateIdentities inside those files
//     [LoggerMessage(
//         EventId = 1308,
//         Level = LogLevel.Warning,
//         Message = "Candidate identity {CandidateIdentityId} already exists with different canonical " +
//                   "attributes (full name differs: {FullNameDiffers}, birth date differs: {BirthDateDiffers}). " +
//                   "The stored identity was preserved and the incoming attributes were not applied.")]
//     public static partial void CandidateIdentityConflict(
//         ILogger logger,
//         Guid candidateIdentityId,
//         bool fullNameDiffers,
//         bool birthDateDiffers);
// }
