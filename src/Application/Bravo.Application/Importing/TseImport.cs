using System.Globalization;

namespace Bravo.Application.Importing;

/// <summary>
/// TSE import stages and the scoped unit-key format they use.
/// </summary>
/// <remarks>
/// The scope is encoded in the work-unit key because <c>ImportWorkUnits.UnitKey</c> is
/// already durable and unique per job. This avoids a second table purely to carry scope.
/// Format: <c>{stage}:{year}:{state}:{round}:{electionCode}</c>.
/// </remarks>
public static class TseImport
{
    public const string ReferenceStage = "TseReference";
    public const string CandidatesStage = "TseCandidates";
    public const string ResultsStage = "TseResults";

    private const char Separator = ':';

    public static string Build(string stage, int year, string state, short round, int electionCode)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{stage}{Separator}{year}{Separator}{state.ToUpperInvariant()}{Separator}{round}{Separator}{electionCode}");

    public static string[] KeysFor(int year, string state, short round, int electionCode) =>
    [
        Build(ReferenceStage, year, state, round, electionCode),
        Build(CandidatesStage, year, state, round, electionCode),
        Build(ResultsStage, year, state, round, electionCode)
    ];

    public static bool TryParse(
        string unitKey,
        out string stage,
        out int year,
        out string state,
        out short round,
        out int electionCode)
    {
        stage = string.Empty;
        year = 0;
        state = string.Empty;
        round = 0;
        electionCode = 0;

        var parts = unitKey.Split(Separator);

        if (parts.Length != 5)
            return false;

        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out year))
            return false;

        if (!short.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out round))
            return false;

        if (!int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out electionCode))
            return false;

        stage = parts[0];
        state = parts[2];

        return true;
    }

    public static bool IsTseUnitKey(string unitKey)
        => unitKey.StartsWith(ReferenceStage, StringComparison.Ordinal)
           || unitKey.StartsWith(CandidatesStage, StringComparison.Ordinal)
           || unitKey.StartsWith(ResultsStage, StringComparison.Ordinal);
}
