using Bravo.Application.Tse.Models;
using Microsoft.Extensions.Logging;

namespace Bravo.Application.Imports.Tse.Services;

/// <summary>
/// Diagnostics specific to the TSE import stages.
/// </summary>
/// <remarks>
/// Rejections are aggregated per row with the reason and the scope, never with the CPF or any other
/// value that could identify a person. Counting them is what lets the job settle as
/// <c>CompletedWithErrors</c> instead of silently discarding source records.
/// </remarks>
public static partial class TseImportLog
{
    [LoggerMessage(
        EventId = 1231,
        Level = LogLevel.Debug,
        Message = "Rejected a TSE result row in {Year}/{State}: {Reason}.")]
    public static partial void ResultRowRejected(
        ILogger logger,
        int year,
        string state,
        TseResultRejection reason
    );
}
