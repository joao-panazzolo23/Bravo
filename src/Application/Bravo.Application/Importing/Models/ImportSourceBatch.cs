using Bravo.Application.Importing.Models;

namespace Bravo.Application.Importing.Models;

public sealed record ImportSourceBatch<TRecord>(
    IReadOnlyList<TRecord> Records,
    IReadOnlyList<ImportRejectedRecord> RejectedRecords,
    string? NextCursor,
    bool HasMore);
