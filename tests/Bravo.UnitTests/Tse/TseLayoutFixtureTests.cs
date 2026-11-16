using Bravo.Infrastructure.Tse.Layouts;
using Bravo.Infrastructure.Tse.Layouts.Candidates;
using Bravo.Infrastructure.Tse.Readers;
using FluentAssertions;

namespace Bravo.UnitTests.Tse;

/// <summary>
/// Verifies the 2022 candidate layout against a fixture containing the real TSE header and
/// one real data row, so the canonical column names stay anchored to production data.
/// </summary>
public sealed class TseLayoutFixtureTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "Tse", "Fixtures", "consulta_cand_2022_sample.csv");

    [Fact]
    public void ParsesRealTseHeaderAndRow()
    {
        File.Exists(FixturePath).Should().BeTrue($"fixture must be copied to output: {FixturePath}");

        using var stream = File.OpenRead(FixturePath);

        var records = new TseRecordReader(CandidateLayouts.V2022()).Read(stream).ToList();

        records.Should().ContainSingle();

        var record = records[0];
        record.Values[Canonical.Year].Should().Be("2022");
        record.Values[Canonical.StateAcronym].Should().Be("AC");
        record.Values[Canonical.ElectionTypeName].Should().Contain("ELEI");
        record.Values[Canonical.CandidateSequential].Should().NotBeNullOrWhiteSpace();
        record.Values[Canonical.PartyAcronym].Should().NotBeNullOrWhiteSpace();
        record.Values[Canonical.CandidateName].Should().NotBeNullOrWhiteSpace();
        record.Values[Canonical.AggregationType].Should().NotBeNullOrWhiteSpace();

        // Proves the file is decoded as Latin-1 (accented text) rather than mangled bytes.
        record.Values[Canonical.ElectionTypeName].Should().Be("ELEIÇÃO ORDINÁRIA");
    }
}
