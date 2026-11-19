using Bravo.Domain.Importing.Enums;

namespace Bravo.Domain.Importing.Models;

public sealed record ImportRejectedRecord(
    string SourceIdentifier,
    string? Payload,
    ImportErrorCategory Category,
    string Error);
