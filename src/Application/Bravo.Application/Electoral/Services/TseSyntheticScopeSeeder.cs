using Bravo.Application.Logging;
using Bravo.Domain.Electoral.Services;
using Bravo.Domain.Geography.Entities;
using Microsoft.Extensions.Logging;
using Bravo.Domain.Geography.Repositories;

namespace Bravo.Application.Electoral.Services;

/// <summary>
/// Ensures the non-IBGE geographic scopes TSE publishes (<c>BR</c>, <c>BRASIL</c>, <c>ZZ</c>)
/// exist as state rows so candidates and results can reference them.
/// </summary>
/// <remarks>
/// Synthetic rows use negative IBGE codes, which real IBGE codes (11–53) can never occupy.
/// This keeps them distinguishable from authoritative geography while preserving referential
/// integrity instead of silently dropping those candidates and votes.
/// </remarks>
public sealed class TseSyntheticScopeSeeder(
    IGeographyRepository geography,
    ILogger<TseSyntheticScopeSeeder> logger)
{
    private const int NationalCode = -1;
    private const int PresidentialCode = -2;
    private const int ExteriorCode = -3;

    public async Task<Guid> EnsureAsync(
        string scope,
        Func<IReadOnlyCollection<State>, CancellationToken, Task<int>> upsert,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        var code = scope switch
        {
            TseScopes.National => NationalCode,
            TseScopes.Presidential => PresidentialCode,
            TseScopes.Exterior => ExteriorCode,
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Not a synthetic scope.")
        };

        var existing = await geography.GetStateIdsByAcronymAsync(cancellationToken);

        if (existing.TryGetValue(scope, out var id))
            return id;

        var name = TseScopes.Synthetic.First(entry => entry.Abbreviation == scope).Name;

        await upsert([State.Create(code, scope, name, "NACIONAL")], cancellationToken);

        var refreshed = await geography.GetStateIdsByAcronymAsync(cancellationToken);

        if (!refreshed.TryGetValue(scope, out var created))
            throw new InvalidOperationException($"Failed to create synthetic scope '{scope}'.");

        ValidationLog.SyntheticScopeCreated(logger, scope);

        return created;
    }
}
