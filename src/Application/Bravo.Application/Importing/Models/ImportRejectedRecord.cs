using Bravo.Domain.Importing.Enums;

namespace Bravo.Application.Importing.Models;

public sealed record ImportRejectedRecord(
    string SourceIdentifier,
    string? Payload,
    ImportErrorCategory Category,
    string Error);
