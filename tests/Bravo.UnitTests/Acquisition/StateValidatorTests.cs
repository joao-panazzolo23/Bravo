using Bravo.Infrastructure.Acquisition.Validators;
using Bravo.Domain.Geography.Entities;
using FluentAssertions;

namespace Bravo.UnitTests.Acquisition;

public sealed class StateValidatorTests
{
    private readonly StateValidator _validator = new();

    [Fact]
    public void ValidStateIsAccepted()
    {
        var state = State.Create(35, "SP", "São Paulo", "SE");

        _validator.Validate(state).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, "SP", "São Paulo", "SE")]
    [InlineData(-1, "SP", "São Paulo", "SE")]
    [InlineData(35, "SPX", "São Paulo", "SE")]
    [InlineData(35, "SP", "", "SE")]
    [InlineData(35, "SP", "São Paulo", "")]
    public void InvalidStateIsRejected(int ibgeCode, string abbreviation, string name, string region)
    {
        var state = State.Create(ibgeCode, abbreviation, name, region);

        _validator.Validate(state).IsValid.Should().BeFalse();
    }

    [Fact]
    public void OverlongStateNameIsRejected()
    {
        var state = State.Create(35, "SP", new string('a', 101), "SE");

        _validator.Validate(state).IsValid.Should().BeFalse();
    }
}
