using System.Globalization;

namespace Bravo.Application.Importing;

/// <summary>
/// TSE import stages and the unit-key format each stage uses.
/// </summary>
/// <remarks>
/// Work-unit keys are durable and unique per job, so the import scope is encoded directly in
/// the key. This lets a multi-hour import resume per (year, state) without a second table.
/// Formats:
/// <list type="bullet">
/// <item><c>TseCandidates:{year}</c> — one unit per candidate archive.</item>
/// <item><c>TseResults:{year}:{state}</c> — one unit per (year, state) results archive.</item>
/// </list>
/// </remarks>
public static class TseImport
{
    public const string ReferenceStage = "TseReference";
    public const string CandidatesStage = "TseCandidates";
    public const string ResultsStage = "TseResults";

    private const char Separator = ':';

    public static string ReferenceKey(int year)
        => string.Create(CultureInfo.InvariantCulture, $"{ReferenceStage}{Separator}{year}");

    public static string CandidatesKey(int year)
        => string.Create(CultureInfo.InvariantCulture, $"{CandidatesStage}{Separator}{year}");

    public static string ResultsKey(int year, string state)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{ResultsStage}{Separator}{year}{Separator}{state.ToUpperInvariant()}");

    public static bool TryParseYear(string unitKey, string stage, out int year)
    {
        year = 0;

        var parts = unitKey.Split(Separator);

        return parts.Length == 2
               && parts[0] == stage
               && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out year);
    }

    public static bool TryParseResults(string unitKey, out int year, out string state)
    {
        year = 0;
        state = string.Empty;

        var parts = unitKey.Split(Separator);

        if (parts.Length != 3 || parts[0] != ResultsStage)
            return false;

        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out year))
            return false;

        state = parts[2];

        return true;
    }

    public static bool IsTseUnitKey(string unitKey)
        => unitKey.StartsWith(ReferenceStage, StringComparison.Ordinal)
           || unitKey.StartsWith(CandidatesStage, StringComparison.Ordinal)
           || unitKey.StartsWith(ResultsStage, StringComparison.Ordinal);

    /// <summary>
    /// Detects the retired five-part key format (<c>Stage:Year:State:Round:ElectionCode</c>)
    /// that an earlier planner emitted.
    /// </summary>
    /// <remarks>
    /// Rows created by that planner still exist in deployed databases. They are not silently
    /// accepted or reclassified: the caller reports them as invalid so an operator can remove
    /// them, because rewriting an old key into the new format would guess at its intent.
    /// </remarks>
    public static bool IsLegacyKey(string unitKey)
        => IsTseUnitKey(unitKey) && unitKey.Split(Separator).Length > 3;

    /// <summary>
    /// Human-readable reason a key was rejected, used in failure messages.
    /// </summary>
    public static string DescribeInvalidKey(string unitKey)
        => IsLegacyKey(unitKey)
            ? $"'{unitKey}' uses the retired five-part work-unit key format; delete work units created before the current planner and re-run the import."
            : $"'{unitKey}' does not match the '{CandidatesStage}:<year>' or '{ResultsStage}:<year>:<state>' format.";
}
