using Bravo.Domain.Electoral.Models;

namespace Bravo.Domain.Electoral.Services;

/// <summary>
/// Derives the natural identity key the database enforces for <c>CandidateIdentities</c>.
/// </summary>
/// <remarks>
/// A candidate identity is unique by CPF when one is present and by (full name, birth date)
/// when it is not. The key must normalise CPF exactly as <c>CandidateIdentity.Create</c> does:
/// anything that does not reduce to eleven digits is treated as absent, otherwise the importer
/// would cache a row the database never stores under that value.
/// </remarks>
public static class CandidateIdentityKey
{
    public static string For(TseCandidateRow row)
        => Of(row.Cpf, row.Name, row.BirthDate);

    public static string Of(string? cpf, string fullName, DateOnly? birthDate)
    {
        var normalised = NormaliseCpf(cpf);

        return normalised is null
            ? $"name:{fullName}:{birthDate:yyyy-MM-dd}"
            : $"cpf:{normalised}";
    }

    /// <summary>
    /// Returns the eleven-digit CPF, or null when the value is absent or unusable. Mirrors
    /// <c>CandidateIdentity.Create</c>, which is the only place that decides this.
    /// </summary>
    public static string? NormaliseCpf(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return null;

        var digits = new string(cpf.Where(char.IsAsciiDigit).ToArray());

        return digits.Length == 11 ? digits : null;
    }
}
