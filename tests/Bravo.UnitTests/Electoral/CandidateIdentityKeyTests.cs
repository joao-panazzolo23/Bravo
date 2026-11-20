using Bravo.Domain.Electoral.Services;
using FluentAssertions;

namespace Bravo.UnitTests.Electoral;

/// <summary>
/// The identity key decides which candidates share one permanent person row, so it must agree
/// with the entity's CPF normalisation exactly. A mismatch would either split one person into
/// several rows or merge different people.
/// </summary>
public sealed class CandidateIdentityKeyTests
{
    [Fact]
    public void ValidCpfIsKeyedByValue()
    {
        CandidateIdentityKey.Of("12345678901", "ANY NAME", null).Should().Be("cpf:12345678901");
    }

    [Fact]
    public void FormattedCpfIsNormalisedToDigits()
    {
        CandidateIdentityKey.Of("123.456.789-01", "ANY NAME", null)
            .Should().Be("cpf:12345678901");
    }

    [Fact]
    public void CpfWithSurroundingWhitespaceIsNormalised()
    {
        CandidateIdentityKey.Of("  12345678901  ", "ANY NAME", null)
            .Should().Be("cpf:12345678901");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-4")]
    [InlineData("#NULO")]
    [InlineData("123")]              // too short
    [InlineData("123456789012")]     // too long
    public void UnusableCpfFallsBackToNameAndBirthDate(string? cpf)
    {
        CandidateIdentityKey.Of(cpf, "MARIA DA SILVA", new DateOnly(1980, 5, 4))
            .Should().Be("name:MARIA DA SILVA:1980-05-04");
    }

    [Fact]
    public void MissingBirthDateStillProducesAStableKey()
    {
        CandidateIdentityKey.Of(null, "JOSE SEM DATA", null)
            .Should().Be("name:JOSE SEM DATA:");
    }

    [Fact]
    public void SamePersonWithCpfAndWithoutCpfAreNotMerged()
    {
        // Two source rows for one person: one carries a CPF, the other does not. They cannot be
        // proven to be the same person, so they must remain separate keys rather than silently
        // collapsing into one identity.
        var withCpf = CandidateIdentityKey.Of("12345678901", "MARIA DA SILVA", null);
        var withoutCpf = CandidateIdentityKey.Of(null, "MARIA DA SILVA", null);

        withCpf.Should().NotBe(withoutCpf);
    }

    [Fact]
    public void DifferentPeopleWithTheSameNameDoNotShareAKey()
    {
        var first = CandidateIdentityKey.Of(null, "JOSE PEREIRA", new DateOnly(1970, 1, 1));
        var second = CandidateIdentityKey.Of(null, "JOSE PEREIRA", new DateOnly(1980, 1, 1));

        first.Should().NotBe(second);
    }

    [Fact]
    public void KeyMatchesNormalisationUsedByTheIdentityEntity()
    {
        // CandidateIdentity.Create is the authority on what the database stores. The key for
        // every input that entity turns into a CPF row must be a cpf: key, and vice versa.
        var cases = new[]
        {
            "12345678901", "123.456.789-01", " 12345678901 ", "-4", "#NULO", "", "123", null
        };

        foreach (var cpf in cases)
        {
            var identity = Bravo.Domain.Electoral.Entities.CandidateIdentity.Create(
                cpf, "PERSON", new DateOnly(1990, 1, 1));

            var key = CandidateIdentityKey.Of(cpf, identity.FullName, identity.BirthDate);

            if (identity.Cpf is null)
            {
                key.Should().StartWith("name:");
            }
            else
            {
                key.Should().Be($"cpf:{identity.Cpf}");
            }
        }
    }
}
