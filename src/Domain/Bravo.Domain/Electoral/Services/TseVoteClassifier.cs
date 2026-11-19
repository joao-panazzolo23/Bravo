using Bravo.Domain.Electoral.Enums;
namespace Bravo.Domain.Electoral.Services;

/// <summary>
/// Classifies a raw TSE ballot number into an explicit vote type.
/// </summary>
/// <remarks>
/// Verified against real data: <c>NR_VOTAVEL</c> 95 is always "VOTO BRANCO" and 96 is always
/// "VOTO NULO"; both carry <c>SQ_CANDIDATO = -1</c>. A row is a candidate vote only when a
/// candidate sequential number is present. These sentinels must never be read as candidate
/// identifiers.
/// </remarks>
public static class TseVoteClassifier
{
    public const int BlankVoteCode = 95;
    public const int NullVoteCode = 96;

    public static VoteType Classify(int ballotNumber, string? candidateSequential)
    {
        if (ballotNumber == BlankVoteCode)
            return VoteType.Blank;

        if (ballotNumber == NullVoteCode)
            return VoteType.Null;

        if (!string.IsNullOrWhiteSpace(candidateSequential))
            return VoteType.Nominal;

        // Remaining ballots without a candidate sequential are party/legend votes.
        return VoteType.Party;
    }
}
