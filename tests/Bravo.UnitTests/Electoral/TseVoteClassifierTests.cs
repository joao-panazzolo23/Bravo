using Bravo.Domain.Electoral.Models;
using Bravo.Domain.Electoral.Enums;
using FluentAssertions;
using Bravo.Domain.Electoral.Services;

namespace Bravo.UnitTests.Electoral;

public sealed class TseVoteClassifierTests
{
    [Fact]
    public void BlankCodeIsBlankRegardlessOfOtherValues()
    {
        TseVoteClassifier.Classify(95, candidateSequential: null).Should().Be(VoteType.Blank);
    }

    [Fact]
    public void NullCodeIsNullRegardlessOfOtherValues()
    {
        TseVoteClassifier.Classify(96, candidateSequential: null).Should().Be(VoteType.Null);
    }

    [Fact]
    public void SentinelCodeIsNeverTreatedAsCandidate()
    {
        TseVoteClassifier.Classify(95, candidateSequential: "12345").Should().Be(VoteType.Blank);
        TseVoteClassifier.Classify(96, candidateSequential: "12345").Should().Be(VoteType.Null);
    }

    [Fact]
    public void BallotWithCandidateSequentialIsNominal()
    {
        TseVoteClassifier.Classify(50510, "10001595357").Should().Be(VoteType.Nominal);
    }

    [Fact]
    public void BallotWithoutCandidateSequentialIsParty()
    {
        TseVoteClassifier.Classify(50, candidateSequential: null).Should().Be(VoteType.Party);
    }
}
