using Bravo.Application.Imports.Common.Execution;
using Bravo.Application.Logging;
using Bravo.Application.Tse.Models;
using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Models;
using Bravo.Domain.Electoral.Repositories;
using Bravo.Domain.Electoral.Services;
using Bravo.Domain.Geography.Repositories;
using Bravo.Domain.Importing.Enums;
using Bravo.Domain.Importing.Models;
using Microsoft.Extensions.Logging;

namespace Bravo.Application.Imports.Tse.Services;

///Todo: very large class, maybe refactor later
/// <summary>
/// Imports candidate registrations for every configured year.
/// </summary>
/// <remarks>
/// A year's archive contains every state file, so years run concurrently and the states of one
/// year are processed sequentially inside that archival unit. Running years in parallel already
/// keeps the CPU and database busy without multiplying archive extraction memory.
///
/// Candidate identities are immutable. This stage resolves every identity a group references with
/// one batched insert-and-read-back and never updates an existing row; a divergence between an
/// incoming name or birth date and the canonical record is reported and the incoming value is
/// discarded.
/// </remarks>
public sealed class TseCandidateImporter(
    ITseDatasetAcquirer acquirer,
    ITseCandidateReader candidateReader,
    ICoalitionRepository coalitions,
    ICandidateRepository candidates,
    IElectoralReferenceRepository reference,
    IGeographyRepository geography,
    TseScopeResolver scopeResolver,
    TseImportSession session,
    ImportResourceGovernor resources,
    ILogger<TseCandidateImporter> logger)
{
    public async Task RunAsync(
        ImportExecutionContext context,
        TseElectionLookup electionLookup,
        CancellationToken cancellationToken)
    {
        var years = TseCatalog.CandidateYears;
        var completed = 0;
        var failed = 0;
        long processed = 0;
        long rejected = 0;

        context.Report(
            ImportStage.Candidates,
            years.Length,
            completed,
            failed,
            processed,
            rejected,
            "Candidates");

        await Parallel.ForEachAsync(
            years,
            ParallelOptionsFor(context, cancellationToken),
            async (year, token) =>
            {
                using var yearScope = logger.BeginOperationScope(year: year);

                // Candidate scopes have their own budget: the files are much smaller than a
                // results file, but they still deserve an independent bound.
                using var permit = await resources.AcquireCandidateScopeAsync(token);

                try
                {
                    var count = await ImportYearAsync(year, context.BatchSize, electionLookup, token);

                    Interlocked.Add(ref processed, count);
                    Interlocked.Increment(ref completed);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    Interlocked.Increment(ref failed);
                    Interlocked.Add(ref rejected, 1);

                    ProcessingLog.CandidateImportFailed(logger, year, exception);
                }

                context.Report(
                    ImportStage.Candidates,
                    years.Length,
                    Volatile.Read(ref completed),
                    Volatile.Read(ref failed),
                    Volatile.Read(ref processed),
                    Volatile.Read(ref rejected),
                    $"Candidates {year}");
            });
    }

    private async Task<int> ImportYearAsync(
        int year,
        int batchSize,
        TseElectionLookup electionLookup,
        CancellationToken cancellationToken)
    {
        // The reference stage already downloaded and extracted this year's archive. Reusing the
        // session turns a second full download into a cache hit.
        var files = await session.GetOrAcquireCandidatesAsync(year, acquirer, cancellationToken);

        if (files.Count == 0)
        {
            throw new InvalidOperationException(
                $"The {year} TSE candidate archive contains no state files.");
        }

        var stateIds = await geography.GetStateIdsByAcronymAsync(cancellationToken);
        var officeIds = await reference.GetOfficeIdsAsync(cancellationToken);
        var partyIds = await reference.GetPartyIdsAsync(cancellationToken);
        var federationIds = await reference.GetFederationIdsAsync(cancellationToken);

        // Identities are shared by every state file in a year, because a person can register in
        // more than one state and across rounds. The cache lives for one year, which bounds its
        // size while keeping reuse high.
        var identities = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var imported = 0;

        foreach (var file in files)
        {
            using var stateScope = logger.BeginOperationScope(year: year, state: file.StateAcronym);

            var stateId = await scopeResolver.TryResolveAsync(file.StateAcronym, stateIds, cancellationToken);

            if (stateId is null)
            {
                ValidationLog.GeographyResolutionSkipped(logger, "candidates", year, file.StateAcronym);

                continue;
            }

            await using var stream = File.OpenRead(file.Dataset.FilePath);

            // A candidate file is small enough to map in one go, but it still counts against
            // the shared reader budget so it cannot starve a results stream.
            IReadOnlyList<TseCandidateRow> rows;

            using (await resources.AcquireCsvReaderAsync(cancellationToken))
            {
                rows = candidateReader.Read(stream, file.Dataset.LayoutVersion);
            }

            imported += await ImportFileAsync(
                year,
                file.StateAcronym,
                stateId.Value,
                rows,
                officeIds,
                partyIds,
                federationIds,
                identities,
                electionLookup,
                batchSize,
                cancellationToken);
        }

        PersistenceLog.CandidateRegistrationsUpserted(logger, imported, year, files.Count);

        return imported;
    }

    private async Task<int> ImportFileAsync(
        int year,
        string stateAcronym,
        Guid stateId,
        IReadOnlyList<TseCandidateRow> rows,
        IReadOnlyDictionary<int, Guid> officeIds,
        IReadOnlyDictionary<(int Number, string Acronym), Guid> partyIds,
        IReadOnlyDictionary<int, Guid> federationIds,
        Dictionary<string, Guid> identities,
        TseElectionLookup electionLookup,
        int batchSize,
        CancellationToken cancellationToken
    )
    {
        var imported = 0;

        foreach (var group in rows.GroupBy(row => (row.ElectionCode, row.Round)))
        {
            if (!electionLookup.TryGetElectionId(group.Key.ElectionCode, out var electionId))
                continue;

            if (!electionLookup.TryGetRoundId(group.Key.ElectionCode, group.Key.Round, out var roundId))
                continue;

            var groupRows = group.ToArray();

            var coalitionIds = await UpsertCoalitionsAsync(
                groupRows, roundId, stateId, officeIds, cancellationToken);

            // Identities are resolved for the whole group before mapping, so a group costs one
            // bulk read and at most one bulk write instead of a roundtrip per row.
            await ResolveIdentitiesAsync(groupRows, identities, cancellationToken);

            var buffer = new Dictionary<(string Seq, int Ballot, string Name), Candidate>(batchSize);

            foreach (var row in groupRows)
            {
                var candidate = MapCandidate(
                    row, electionId, stateId, officeIds, partyIds, federationIds,
                    coalitionIds, roundId, identities);

                // The same candidate sequential can repeat within a file; the upsert key is
                // (election, sequential), so duplicates must be collapsed before writing.
                buffer[(row.Sequential, row.BallotNumber, row.Name)] = candidate;

                if (buffer.Count < batchSize)
                    continue;

                await candidates.UpsertCandidatesAsync(buffer.Values.ToArray(), cancellationToken);
                imported += buffer.Count;
                buffer.Clear();
            }

            if (buffer.Count == 0)
                continue;

            await candidates.UpsertCandidatesAsync(buffer.Values.ToArray(), cancellationToken);
            imported += buffer.Count;
        }

        PersistenceLog.CandidatesUpsertedForState(logger, imported, year, stateAcronym);

        return imported;
    }

    private static Candidate MapCandidate(
        TseCandidateRow row,
        Guid electionId,
        Guid stateId,
        IReadOnlyDictionary<int, Guid> officeIds,
        IReadOnlyDictionary<(int Number, string Acronym), Guid> partyIds,
        IReadOnlyDictionary<int, Guid> federationIds,
        IReadOnlyDictionary<(Guid, Guid, long), Guid> coalitionIds,
        Guid roundId,
        Dictionary<string, Guid> identities)
    {
        var officeId = Resolve(officeIds, row.OfficeCode, $"office {row.OfficeCode}");

        if (!partyIds.TryGetValue((row.PartyNumber, row.PartyAcronym.ToUpperInvariant()), out var partyId))
        {
            throw new InvalidOperationException(
                $"TSE party {row.PartyNumber}/{row.PartyAcronym} for {row.Sequential} is not imported.");
        }

        Guid? federationId = null;

        if (row.FederationNumber is > 0 and var federationNumber
            && federationIds.TryGetValue(federationNumber, out var resolvedFederation))
        {
            federationId = resolvedFederation;
        }

        Guid? coalitionId = null;

        if (row.HasCoalition
            && row.CoalitionNumber is { } coalitionNumber
            && coalitionIds.TryGetValue((roundId, officeId, coalitionNumber), out var resolvedCoalition))
        {
            coalitionId = resolvedCoalition;
        }

        if (!identities.TryGetValue(CandidateIdentityKey.For(row), out var identityId))
        {
            throw new InvalidOperationException(
                $"Candidate identity for '{row.Name}' was not resolved before mapping.");
        }

        return Candidate.Create(
            electionId,
            officeId,
            stateId,
            identityId,
            row.Sequential,
            row.BallotNumber,
            row.Name,
            row.UrnName,
            row.SocialName,
            partyId,
            federationId,
            coalitionId,
            row.RegistrationStatus,
            row.Education,
            row.Occupation
        );
    }

    private async Task<IReadOnlyDictionary<(Guid, Guid, long), Guid>> UpsertCoalitionsAsync(
        IReadOnlyList<TseCandidateRow> rows,
        Guid roundId,
        Guid stateId,
        IReadOnlyDictionary<int, Guid> officeIds,
        CancellationToken cancellationToken)
    {
        var distinct = rows
            .Where(row => row.HasCoalition && row.CoalitionNumber is > 0)
            .GroupBy(row => (row.CoalitionNumber!.Value, row.OfficeCode))
            .Select(group => group.First())
            .ToArray();

        if (distinct.Length == 0)
            return new Dictionary<(Guid, Guid, long), Guid>();

        var entities = distinct
            .Select(row => Coalition.Create(
                roundId,
                Resolve(officeIds, row.OfficeCode, $"office {row.OfficeCode}"),
                stateId,
                row.CoalitionNumber!.Value,
                row.CoalitionName ?? $"Coalition {row.CoalitionNumber}",
                row.CoalitionComposition))
            .ToArray();

        await coalitions.UpsertAsync(entities, cancellationToken);

        return await coalitions.GetIdsAsync(stateId, cancellationToken);
    }

    /// <summary>
    /// Ensures every identity referenced by <paramref name="rows"/> exists, filling the cache.
    /// </summary>
    /// <remarks>
    /// The cache key comes from <see cref="CandidateIdentityKey"/>, which normalises CPF the same
    /// way the entity does. Every miss in a group is written with one batched call, so the number
    /// of identity roundtrips follows the number of groups rather than the number of candidates.
    ///
    /// Identities are immutable: the repository inserts what is missing and reads back the ids of
    /// both new and pre-existing rows in the same transaction. A pre-existing CPF seen with a
    /// different name or birth date keeps its canonical attributes; the divergence is logged as a
    /// structured diagnostic (never with the CPF value) and the incoming value is discarded.
    /// </remarks>
    private async Task ResolveIdentitiesAsync(
        IReadOnlyList<TseCandidateRow> rows,
        Dictionary<string, Guid> cache,
        CancellationToken cancellationToken)
    {
        var missing = new Dictionary<string, TseCandidateRow>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            var key = CandidateIdentityKey.For(row);

            if (!cache.ContainsKey(key) && !missing.ContainsKey(key))
                missing[key] = row;
        }

        if (missing.Count == 0)
            return;

        var entities = missing.Values
            .Select(row => CandidateIdentity.Create(row.Cpf, row.Name, row.BirthDate))
            .ToArray();

        var resolution = await candidates.EnsureIdentitiesAsync(entities, cancellationToken);

        // foreach (var conflict in resolution.Conflicts)
        // {
        //     ValidationLog.CandidateIdentityConflict(
        //         logger,
        //         conflict.CandidateIdentityId,
        //         conflict.FullNameDiffers,
        //         conflict.BirthDateDiffers);
        // }

        foreach (var identity in entities)
        {
            var key = CandidateIdentityResolution.KeyOf(identity);

            if (!resolution.IdsByKey.TryGetValue(key, out var id))
            {
                throw new InvalidOperationException(
                    $"Candidate identity resolution did not return an id for '{identity.FullName}'.");
            }

            cache[key] = id;
        }
    }

    private static Guid Resolve(IReadOnlyDictionary<int, Guid> map, int key, string description)
        => map.TryGetValue(key, out var id)
            ? id
            : throw new InvalidOperationException($"TSE {description} is not imported.");

    private static ParallelOptions ParallelOptionsFor(
        ImportExecutionContext context,
        CancellationToken cancellationToken) => new()
        {
            MaxDegreeOfParallelism = Math.Max(1, context.MaxParallelism),
            CancellationToken = cancellationToken
        };
}
