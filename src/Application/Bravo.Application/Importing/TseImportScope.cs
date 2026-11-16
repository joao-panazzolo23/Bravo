namespace Bravo.Application.Importing;

/// <summary>
/// Convenience view over a parsed TSE unit key.
/// </summary>
public sealed record TseImportScope(
    string Stage,
    int Year,
    string State,
    short Round,
    int ElectionCode)
{
    public static TseImportScope Parse(string unitKey)
    {
        if (!TseImport.TryParse(unitKey, out var stage, out var year, out var state, out var round, out var electionCode))
            throw new FormatException($"'{unitKey}' is not a valid TSE unit key.");

        return new TseImportScope(stage, year, state, round, electionCode);
    }
}
