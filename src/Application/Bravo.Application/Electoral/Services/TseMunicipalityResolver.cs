using Bravo.Application.Electoral.Contracts;
using Bravo.Domain.Electoral.Enums;
using Bravo.Domain.Electoral.Entities;

namespace Bravo.Application.Electoral.Services;

/// <summary>
/// Resolves TSE municipality codes to IBGE municipalities without inventing relationships.
/// </summary>
/// <remarks>
/// A match is accepted automatically only when the normalised name maps to exactly one
/// municipality within the same state. Ambiguous or missing matches stay unresolved and are
/// stored with a low confidence so they can be reviewed.
/// </remarks>
public sealed class TseMunicipalityResolver(IGeographyLookup geography)
{
    public const decimal ExactConfidence = 1.00m;
    public const decimal NameConfidence = 0.60m;
    public const decimal UnresolvedConfidence = 0.00m;

    public async Task<IReadOnlyList<TseMunicipalityCode>> ResolveAsync(
        IEnumerable<(string RawCode, string MunicipalityName, Guid StateId)> observations,
        int electionYear,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observations);

        var municipalities = await geography.GetMunicipalitiesByStateAsync(cancellationToken);

        var byStateAndName = municipalities
            .GroupBy(municipality => municipality.StateId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .GroupBy(municipality => MunicipalityNameNormaliser.Normalise(municipality.Name))
                    .ToDictionary(nameGroup => nameGroup.Key, nameGroup => nameGroup.ToArray()));

        var results = new List<TseMunicipalityCode>();

        foreach (var observation in observations
                     .GroupBy(item => (item.RawCode, item.StateId))
                     .Select(group => group.First()))
        {
            if (!byStateAndName.TryGetValue(observation.StateId, out var names))
            {
                results.Add(Create(observation, electionYear, null, MunicipalityMatchMethod.Unresolved, UnresolvedConfidence));
                continue;
            }

            var key = MunicipalityNameNormaliser.Normalise(observation.MunicipalityName);

            if (!names.TryGetValue(key, out var candidates) || candidates.Length == 0)
            {
                results.Add(Create(observation, electionYear, null, MunicipalityMatchMethod.Unresolved, UnresolvedConfidence));
                continue;
            }

            if (candidates.Length > 1)
            {
                results.Add(Create(observation, electionYear, null, MunicipalityMatchMethod.Unresolved, UnresolvedConfidence));
                continue;
            }

            results.Add(Create(
                observation,
                electionYear,
                candidates[0].Id,
                MunicipalityMatchMethod.NormalizedName,
                NameConfidence));
        }

        return results;
    }

    private static TseMunicipalityCode Create(
        (string RawCode, string MunicipalityName, Guid StateId) observation,
        int electionYear,
        Guid? municipalityId,
        MunicipalityMatchMethod method,
        decimal confidence)
        => TseMunicipalityCode.Create(
            observation.RawCode,
            observation.StateId,
            municipalityId,
            electionYear,
            method,
            confidence);
}
