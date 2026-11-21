using Bravo.Shared.Entities;

namespace Bravo.Domain.Electoral.Entities;

/// <summary>
/// Permanent candidate identity, independent of any single election. Keyed by CPF when the
/// source provides one, because TSE candidate sequential numbers are election-scoped.
/// </summary>
public class CandidateIdentity : Entity
{
    public string? Cpf { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public DateOnly? BirthDate { get; private set; }

    private CandidateIdentity() { }

    public static CandidateIdentity Create(
        string? cpf,
        string fullName,
        DateOnly? birthDate
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);

        var identity = new CandidateIdentity
        {
            Cpf = NormaliseCpf(cpf),
            FullName = fullName,
            BirthDate = birthDate
        };

        var now = DateTime.UtcNow;
        identity.SetCreatedAt(now);
        identity.SetUpdatedAt(now);

        return identity;
    }

    private static string? NormaliseCpf(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return null;

        var digits = new string(cpf.Where(char.IsAsciiDigit).ToArray());

        return digits.Length == 11 ? digits : null;
    }
}
