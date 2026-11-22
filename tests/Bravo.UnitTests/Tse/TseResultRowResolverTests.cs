using Bravo.Application.Imports.Tse.Models;
using Bravo.Application.Imports.Tse.Services;
using Bravo.Application.Tse.Models;
using Bravo.Domain.Electoral.Enums;
using Bravo.Domain.Electoral.Models;
using FluentAssertions;

namespace Bravo.UnitTests.Tse;

/// <summary>
/// Pins the row-to-key resolution that used to be an untestable struct nested in the orchestrator.
/// </summary>
/// <remarks>
/// Every rejection reason is asserted explicitly. Silently returning <c>false</c> was the original
/// defect: the import skipped the row and still reported success.
/// </remarks>
public sealed class TseResultRowResolverTests
{
    private static readonly Guid StateId = Guid.CreateVersion7();
    private static readonly Guid MunicipalityId = Guid.CreateVersion7();
    private static readonly Guid ZoneId = Guid.CreateVersion7();
    private static readonly Guid SectionId = Guid.CreateVersion7();
    private static readonly Guid OfficeId = Guid.CreateVersion7();
    private static readonly Guid ElectionId = Guid.CreateVersion7();
    private static readonly Guid RoundId = Guid.CreateVersion7();
    private static readonly Guid CandidateId = Guid.CreateVersion7();
    private static readonly Guid PartyId = Guid.CreateVersion7();

    private const int ElectionCode = 2022;
    private const string CandidateSequential = "12345";

    private static TseResultRowResolver BuildResolver()
        => new(
            StateId,
            new Dictionary<string, Guid?> { ["MUN-1"] = MunicipalityId },
            new TseStructureIds(
                new Dictionary<(Guid, int), Guid> { [(StateId, 10)] = ZoneId },
                new Dictionary<(Guid, Guid, int), Guid> { [(MunicipalityId, ZoneId, 20)] = SectionId },
                new Dictionary<(Guid, Guid?, int), Guid>()),
            new Dictionary<int, Guid> { [101] = OfficeId },
            new Dictionary<int, Guid> { [13] = PartyId },
            BuildLookup(),
            new Dictionary<Guid, IReadOnlyDictionary<string, Guid>>
            {
                [ElectionId] = new Dictionary<string, Guid> { [CandidateSequential] = CandidateId }
            });

    private static TseElectionLookup BuildLookup()
    {
        var lookupType = typeof(TseElectionLookup);
        var constructor = lookupType.GetConstructors(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Single();

        return (TseElectionLookup)constructor.Invoke([
            new Dictionary<int, Guid> { [ElectionCode] = ElectionId },
            new Dictionary<(int, short), Guid> { [(ElectionCode, 1)] = RoundId }
        ]);
    }

    private static TseSectionResultRow Row(
        string municipality = "MUN-1",
        int zone = 10,
        int section = 20,
        int office = 101,
        int election = ElectionCode,
        short round = 1,
        int ballot = 13,
        string? candidateSequential = CandidateSequential,
        long votes = 10)
        => new(
            Year: 2022,
            Round: round,
            ElectionCode: election,
            StateAcronym: "SP",
            TseMunicipalityCode: municipality,
            MunicipalityName: "MUNICIPALITY",
            ZoneNumber: zone,
            SectionNumber: section,
            OfficeCode: office,
            BallotNumber: ballot,
            BallotName: "BALLOT",
            Votes: votes,
            PollingPlaceNumber: 1,
            CandidateSequential: candidateSequential);

    [Fact]
    public void NominalVoteResolvesToTheCandidate()
    {
        var resolver = BuildResolver();

        resolver.TryResolve(Row(), out var key, out var rejection).Should().BeTrue();

        rejection.Should().Be(TseResultRejection.None);
        key.Type.Should().Be(VoteType.Nominal);
        key.CandidateId.Should().Be(CandidateId);
        key.PartyId.Should().BeNull();
        key.RoundId.Should().Be(RoundId);
        key.SectionId.Should().Be(SectionId);
        key.OfficeId.Should().Be(OfficeId);
        key.Ballot.Should().Be(13);
    }

    [Fact]
    public void PartyVoteResolvesToTheParty()
    {
        var resolver = BuildResolver();

        // No candidate sequential means the ballot belongs to the party/legend.
        resolver.TryResolve(Row(candidateSequential: null), out var key, out var rejection)
            .Should().BeTrue();

        rejection.Should().Be(TseResultRejection.None);
        key.Type.Should().Be(VoteType.Party);
        key.PartyId.Should().Be(PartyId);
        key.CandidateId.Should().BeNull();
    }

    [Theory]
    [InlineData(95, VoteType.Blank)]
    [InlineData(96, VoteType.Null)]
    public void BlankAndNullVotesResolveWithoutCandidateOrParty(int ballot, VoteType expected)
    {
        var resolver = BuildResolver();

        resolver.TryResolve(
            Row(ballot: ballot, candidateSequential: null), out var key, out var rejection)
            .Should().BeTrue();

        rejection.Should().Be(TseResultRejection.None);
        key.Type.Should().Be(expected);
        key.CandidateId.Should().BeNull();
        key.PartyId.Should().BeNull();
    }

    [Fact]
    public void UnknownMunicipalityIsRejectedWithItsReason()
    {
        var resolver = BuildResolver();

        resolver.TryResolve(Row(municipality: "UNKNOWN"), out _, out var rejection).Should().BeFalse();

        rejection.Should().Be(TseResultRejection.Municipality);
    }

    [Fact]
    public void UnknownZoneIsRejectedAsAStructureProblem()
    {
        var resolver = BuildResolver();

        resolver.TryResolve(Row(zone: 99), out _, out var rejection).Should().BeFalse();

        rejection.Should().Be(TseResultRejection.Structure);
    }

    [Fact]
    public void UnknownSectionIsRejectedAsAStructureProblem()
    {
        var resolver = BuildResolver();

        resolver.TryResolve(Row(section: 99), out _, out var rejection).Should().BeFalse();

        rejection.Should().Be(TseResultRejection.Structure);
    }

    [Fact]
    public void UnknownOfficeIsRejectedWithItsReason()
    {
        var resolver = BuildResolver();

        resolver.TryResolve(Row(office: 999), out _, out var rejection).Should().BeFalse();

        rejection.Should().Be(TseResultRejection.Office);
    }

    [Fact]
    public void UnknownElectionIsRejectedWithItsReason()
    {
        var resolver = BuildResolver();

        resolver.TryResolve(Row(election: 1994), out _, out var rejection).Should().BeFalse();

        rejection.Should().Be(TseResultRejection.Election);
    }

    [Fact]
    public void UnknownRoundIsRejectedWithItsReason()
    {
        var resolver = BuildResolver();

        resolver.TryResolve(Row(round: 2), out _, out var rejection).Should().BeFalse();

        rejection.Should().Be(TseResultRejection.Election);
    }

    [Fact]
    public void NominalVoteForAnUnknownCandidateIsRejectedRatherThanTreatedAsAPartyVote()
    {
        var resolver = BuildResolver();

        resolver.TryResolve(Row(candidateSequential: "00000"), out _, out var rejection)
            .Should().BeFalse();

        // This is the distinction the old code lost: the ballot says a candidate, so falling
        // through to a party vote would fabricate a result.
        rejection.Should().Be(TseResultRejection.Candidate);
    }

    [Fact]
    public void PartyVoteForAnUnknownPartyIsRejected()
    {
        var resolver = BuildResolver();

        resolver.TryResolve(Row(ballot: 99, candidateSequential: null), out _, out var rejection)
            .Should().BeFalse();

        rejection.Should().Be(TseResultRejection.Party);
    }
}
