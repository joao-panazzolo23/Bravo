using Bravo.Application.Tse.Models;

namespace Bravo.Application.Imports.Tse.Services;

/// <summary>
/// Resolves a TSE scope acronym (a state, or one of the synthetic national/exterior scopes) to a
/// stored state row.
/// </summary>
/// <remarks>
/// Candidates and results need the same rule: use the IBGE state when it exists, and create the
/// synthetic row on demand when TSE publishes a scope IBGE does not model. Keeping it in one place
/// removes the two near-identical helpers the import used to carry.
///
/// This type holds no state; the state map is passed in so the caller can load it once per scope
/// and reuse it for every row.
/// </remarks>
public sealed class TseScopeResolver(TseSyntheticScopeSeeder syntheticScopes)
{
    /// <summary>
    /// Returns the state id for <paramref name="scope"/>, or <see langword="null"/> when the scope
    /// is neither a known state nor a scope the importer is allowed to synthesise.
    /// </summary>
    public async Task<Guid?> TryResolveAsync(
        string scope,
        IReadOnlyDictionary<string, Guid> stateIds,
        CancellationToken cancellationToken = default)
    {
        if (stateIds.TryGetValue(scope, out var existing))
            return existing;

        if (!TseScopes.IsSynthetic(scope))
            return null;

        return await syntheticScopes.EnsureAsync(scope, cancellationToken);
    }

    /// <summary>
    /// Same as <see cref="TryResolveAsync"/> but treats an unknown scope as a hard error, which is
    /// what a results scope requires: silently skipping it would drop a whole state's votes.
    /// </summary>
    public async Task<Guid> ResolveRequiredAsync(
        string scope,
        IReadOnlyDictionary<string, Guid> stateIds,
        CancellationToken cancellationToken = default)
    {
        var resolved = await TryResolveAsync(scope, stateIds, cancellationToken);

        return resolved ?? throw new InvalidOperationException(
            $"TSE state '{scope}' is not present in the geography import.");
    }
}
