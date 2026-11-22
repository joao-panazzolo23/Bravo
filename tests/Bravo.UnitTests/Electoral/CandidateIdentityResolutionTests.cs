using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Models;
using Bravo.Domain.Electoral.Services;
using FluentAssertions;

namespace Bravo.UnitTests.Electoral;

/// <summary>
/// The identity resolution result is the contract between the repository and the candidate
/// import. These tests pin the parts that are pure domain logic: the key used to match a result
/// back to an input row, and the diagnostic shape for an immutable-attribute divergence.
/// </summary>
public sealed class CandidateIdentityResolutionTests
{
    [Fact]
    public void KeyOfMatchesTheNaturalKeyUsedByTheRepository()
    {
        var identity = CandidateIdentity.Create(
            "123.456.789-01", "MARIA DA SILVA", new DateOnly(1980, 5, 4));

        CandidateIdentityResolution.KeyOf(identity)
            .Should().Be(CandidateIdentityKey.Of(identity.Cpf, identity.FullName, identity.BirthDate));

        CandidateIdentityResolution.KeyOf(identity).Should().Be("cpf:12345678901");
    }

    [Fact]
    public void KeyOfUnusableCpfFallsBackToNameAndBirthDate()
    {
        // "-4" is TSE's withheld-CPF sentinel. The entity discards it, so the key must too, or
        // the lookup would ask for a CPF row the database never stores.
        var identity = CandidateIdentity.Create("-4", "JOSE SEM CPF", null);

        CandidateIdentityResolution.KeyOf(identity)
            .Should().Be(CandidateIdentityKey.Of(null, "JOSE SEM CPF", null))
            .And.StartWith("name:");
    }

    [Fact]
    public void EmptyResolutionHasNoIdsAndNoConflicts()
    {
        CandidateIdentityResolution.Empty.IdsByKey.Should().BeEmpty();
        CandidateIdentityResolution.Empty.Conflicts.Should().BeEmpty();
    }

    [Fact]
    public void ConflictCarriesNoSensitiveValue()
    {
        // The diagnostic is logged by the import. It must identify the row without echoing a
        // document number, and the record simply has no field that could carry one.
        var conflict = new CandidateIdentityConflict(Guid.CreateVersion7(), true, false);

        conflict.FullNameDiffers.Should().BeTrue();
        conflict.BirthDateDiffers.Should().BeFalse();
        typeof(CandidateIdentityConflict).GetProperties()
            .Select(property => property.Name)
            .Should().NotContain(name => name.Contains("CPF", StringComparison.OrdinalIgnoreCase));
    }
}
