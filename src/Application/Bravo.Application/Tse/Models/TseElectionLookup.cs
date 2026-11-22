namespace Bravo.Application.Imports.Tse.Models;

/// <summary>
/// Immutable snapshot of every election and round, keyed by TSE election code.
/// </summary>
/// <remarks>
/// Reference data is small (a few hundred elections) and only changes in the reference stage,
/// which always runs before candidates and results. Loading it once per job execution removes
/// the per-row lookup without holding a database connection open. Instances are immutable, so
/// concurrent work units can share one safely.
/// </remarks>
public sealed class TseElectionLookup
{
    private readonly IReadOnlyDictionary<int, Guid> _electionsByCode;
    private readonly IReadOnlyDictionary<(int Code, short Round), Guid> _roundsByCode;

    private TseElectionLookup(
        IReadOnlyDictionary<int, Guid> electionsByCode,
        IReadOnlyDictionary<(int Code, short Round), Guid> roundsByCode)
    {
        _electionsByCode = electionsByCode;
        _roundsByCode = roundsByCode;
    }

    public static async Task<TseElectionLookup> LoadAsync(
        Bravo.Domain.Electoral.Repositories.IElectionRepository elections,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(elections);

        var electionRows = await elections.ListAsync(cancellationToken);
        var roundRows = await elections.ListRoundsAsync(cancellationToken);

        var electionsByCode = new Dictionary<int, Guid>(electionRows.Count);
        var codeById = new Dictionary<Guid, int>(electionRows.Count);

        foreach (var election in electionRows)
        {
            electionsByCode[election.TseElectionCode] = election.Id;
            codeById[election.Id] = election.TseElectionCode;
        }

        var roundsByCode = new Dictionary<(int Code, short Round), Guid>(roundRows.Count);

        foreach (var round in roundRows)
        {
            if (codeById.TryGetValue(round.ElectionId, out var code))
                roundsByCode[(code, round.Round)] = round.Id;
        }

        return new TseElectionLookup(electionsByCode, roundsByCode);
    }

    public bool TryGetElectionId(int electionCode, out Guid electionId)
        => _electionsByCode.TryGetValue(electionCode, out electionId);

    public bool TryGetRoundId(int electionCode, short round, out Guid roundId)
        => _roundsByCode.TryGetValue((electionCode, round), out roundId);
}
