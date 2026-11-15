using Bravo.Domain.Acquisition.Enums;
using Bravo.Shared.Result;

namespace Bravo.Application.Acquisition.Scopes;

/// <summary>
/// Parsed import scope.
///
/// TSE scopes are expressed as <c>&lt;year&gt;</c>, <c>&lt;year&gt;:&lt;round&gt;</c> or
/// <c>&lt;year&gt;:&lt;round&gt;:&lt;UF&gt;</c>. When no state is given, the national
/// aggregate files published by the TSE are used; when a state is given, only that state's
/// files are read. This prevents mixing state and national files, which would count every
/// vote twice.
/// </summary>
public sealed record ImportScope
{
    private static readonly HashSet<string> States = new(StringComparer.OrdinalIgnoreCase)
    {
        "AC", "AL", "AM", "AP", "BA", "CE", "DF", "ES", "GO", "MA", "MG", "MS", "MT",
        "PA", "PB", "PE", "PI", "PR", "RJ", "RN", "RO", "RR", "RS", "SC", "SE", "SP", "TO", "BR"
    };

    private ImportScope(int? year, int? round, string? state)
    {
        Year = year;
        Round = round;
        State = state;
    }

    public int? Year { get; }
    public int? Round { get; }
    public string? State { get; }

    public string Canonical => Year is null
        ? "all"
        : State is null
            ? Round is null ? Year.Value.ToString() : $"{Year.Value}:{Round}"
            : $"{Year.Value}:{Round ?? 1}:{State}";

    public static ImportScope Empty { get; } = new(null, null, null);

    public static Result<ImportScope> Parse(ImportOperationType operationType, string? scope)
    {
        if (operationType is ImportOperationType.IbgeGeography or ImportOperationType.FgvMunicipalityCrosswalk)
        {
            return string.IsNullOrWhiteSpace(scope)
                ? Result<ImportScope>.OK(Empty)
                : Result<ImportScope>.BadRequest(message: "This operation does not accept a scope.");
        }

        if (string.IsNullOrWhiteSpace(scope))
        {
            return Result<ImportScope>.BadRequest(
                message: "A scope with the election year is required for TSE imports. Use '<year>', '<year>:<round>' or '<year>:<round>:<UF>'.");
        }

        var value = scope.Trim();
        var parts = value.Split(':', StringSplitOptions.TrimEntries);

        if (parts.Length is < 1 or > 3 || !int.TryParse(parts[0], out var year))
        {
            return Result<ImportScope>.BadRequest(
                message: "Invalid scope. Expected '<year>', '<year>:<round>' or '<year>:<round>:<UF>'.");
        }

        if (year < 1994 || year > DateTime.UtcNow.Year + 1)
        {
            return Result<ImportScope>.BadRequest(message: $"Unsupported election year '{year}'.");
        }

        int? round = null;
        if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[1]))
        {
            if (!int.TryParse(parts[1], out var parsedRound) || parsedRound is < 1 or > 2)
            {
                return Result<ImportScope>.BadRequest(message: "Invalid round. Only rounds 1 and 2 are supported.");
            }

            round = parsedRound;
        }

        string? state = null;
        if (parts.Length == 3 && !string.IsNullOrWhiteSpace(parts[2]))
        {
            var candidate = parts[2].Trim().ToUpperInvariant();
            if (!States.Contains(candidate))
            {
                return Result<ImportScope>.BadRequest(message: $"Unsupported state '{parts[2]}'.");
            }

            state = candidate;
        }

        return Result<ImportScope>.OK(new ImportScope(year, round, state));
    }
}
