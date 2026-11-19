using Bravo.Domain.Importing.Models;

namespace Bravo.Domain.Importing.Models;

public sealed record ImportSourceBatch<TRecord>(
    IReadOnlyList<TRecord> Records,
    IReadOnlyList<ImportRejectedRecord> RejectedRecords,
    string? NextCursor,
    bool HasMore);
