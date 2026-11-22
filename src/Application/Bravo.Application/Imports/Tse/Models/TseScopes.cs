namespace Bravo.Application.Imports.Tse.Models;

/// <summary>
/// Geographic scopes TSE publishes that IBGE does not model as states.
/// </summary>
/// <remarks>
/// TSE candidate archives contain 29 scopes: the 27 states plus <c>BR</c> (national offices)
/// and <c>BRASIL</c> (presidential). Results archives additionally include <c>ZZ</c>
/// (exterior, from 2026). IBGE publishes only the 27 states, so these three are seeded as
/// explicit rows rather than guessed or silently dropped.
/// </remarks>
public static class TseScopes
{
    public const string National = "BR";
    public const string Presidential = "BRASIL";
    public const string Exterior = "ZZ";

    /// <summary>
    /// Non-IBGE scope rows required before candidates or results referencing them can be
    /// stored. Code 0 marks them as synthetic, so a real IBGE state can never collide.
    /// </summary>
    public static readonly (string Abbreviation, string Name)[] Synthetic =
    [
        (National, "Brasil (nacional)"),
        (Presidential, "Brasil (presidencial)"),
        (Exterior, "Exterior")
    ];

    public static bool IsSynthetic(string scope)
        => scope is National or Presidential or Exterior;
}
