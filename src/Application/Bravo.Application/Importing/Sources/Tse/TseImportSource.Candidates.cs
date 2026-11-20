using Bravo.Application.Logging;
using Bravo.Domain.Importing.Enums;
using Bravo.Application.Electoral;
using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Models;
using Bravo.Domain.Importing.Models;
using Microsoft.Extensions.Logging;

namespace Bravo.Application.Importing.Sources.Tse;

public sealed partial class TseImportSource
{
    /// <summary>
    /// Imports candidate registrations for every year, one year at a time in parallel.
    /// </summary>
    /// <remarks>
    /// A year's archive contains every state file, so states are processed sequentially inside
    /// one archival unit. Running years concurrently is enough to keep the CPU and database busy
    /// without multiplying archive extraction memory.
    /// </remarks>
    private async Task ImportCandidatesAsync(
        ImportExecutionContext context,
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

                try
                {
                    var count = await ImportCandidateYearAsync(year, context.BatchSize, token);

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

    private async Task<int> ImportCandidateYearAsync(
        int year,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var files = await acquirer.AcquireCandidatesAsync(year, cancellationToken);

        if (files.Count == 0)
            throw new InvalidOperationException(
                $"The {year} TSE candidate archive contains no state files.");

        var stateIds = await geography.GetStateIdsByAcronymAsync(cancellationToken);
        var officeIds = await reference.GetOfficeIdsAsync(cancellationToken);
        var partyIds = await reference.GetPartyIdsAsync(cancellationToken);
        var federationIds = await reference.GetFederationIdsAsync(cancellationToken);

        var identities = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var imported = 0;

        foreach (var file in files)
        {
            using var stateScope = logger.BeginOperationScope(year: year, state: file.StateAcronym);

            var stateId = await ResolveStateIdAsync(file.StateAcronym, stateIds, cancellationToken);

            if (stateId is null)
            {
                ValidationLog.GeographyResolutionSkipped(logger, "candidates", year, file.StateAcronym);

                continue;
            }

            await using var stream = File.OpenRead(file.Dataset.FilePath);
            var rows = candidateReader.Read(stream, file.Dataset.LayoutVersion);

            imported += await ImportCandidateFileAsync(
                year,
                file.StateAcronym,
                stateId.Value,
                rows,
                officeIds,
                partyIds,
                federationIds,
                identities,
                batchSize,
                cancellationToken);
        }

        PersistenceLog.CandidateRegistrationsUpserted(logger, imported, year, files.Count);

        return imported;
    }

    private async Task<int> ImportCandidateFileAsync(
        int year,
        string stateAcronym,
        Guid stateId,
        IReadOnlyList<TseCandidateRow> rows,
        IReadOnlyDictionary<int, Guid> officeIds,
        IReadOnlyDictionary<(int Number, string Acronym), Guid> partyIds,
        IReadOnlyDictionary<int, Guid> federationIds,
        Dictionary<string, Guid> identities,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var imported = 0;

        foreach (var group in rows.GroupBy(row => (row.ElectionCode, row.Round)))
        {
            var election = await elections.FindByCodeAsync(group.Key.ElectionCode, cancellationToken);

            if (election is null)
                continue;

            var round = await elections.FindRoundAsync(election.Id, group.Key.Round, cancellationToken);

            if (round is null)
                continue;

            var coalitionIds = await UpsertCoalitionsAsync(
                group.ToArray(), round.Id, stateId, officeIds, cancellationToken);

            var buffer = new Dictionary<(string Seq, int Ballot, string Name), Candidate>(batchSize);

            foreach (var row in group)
            {
                var candidate = await MapCandidateAsync(
                    row, election.Id, stateId, officeIds, partyIds, federationIds,
                    coalitionIds, round.Id, identities, cancellationToken);

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

        PersistenceLog.CandidatesUpsertedForState(
            logger, imported, year, stateAcronym);

        return imported;
    }

    private async Task<Candidate> MapCandidateAsync(
        TseCandidateRow row,
        Guid electionId,
        Guid stateId,
        IReadOnlyDictionary<int, Guid> officeIds,
        IReadOnlyDictionary<(int Number, string Acronym), Guid> partyIds,
        IReadOnlyDictionary<int, Guid> federationIds,
        IReadOnlyDictionary<(Guid, Guid, long), Guid> coalitionIds,
        Guid roundId,
        Dictionary<string, Guid> identities,
        CancellationToken cancellationToken)
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

        var identityId = await ResolveIdentityAsync(row, identities, cancellationToken);

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
            row.Gender,
            row.Education,
            row.Race,
            row.Occupation,
            row.BirthDate);
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

        return await coalitions.GetIdsAsync(cancellationToken);
    }

    /// <summary>
    /// Resolves permanent identity by CPF. Rows without a CPF reuse the identity already stored
    /// for the same name and birth date, so re-running an import does not create a new row.
    /// </summary>
    private async Task<Guid?> ResolveIdentityAsync(
        TseCandidateRow row,
        Dictionary<string, Guid> cache,
        CancellationToken cancellationToken)
    {
        var key = string.IsNullOrWhiteSpace(row.Cpf)
            ? $"name:{row.Name}:{row.BirthDate:yyyy-MM-dd}"
            : $"cpf:{row.Cpf}";

        if (cache.TryGetValue(key, out var cached))
            return cached;

        var identity = await candidates.UpsertIdentityAsync(
            CandidateIdentity.Create(row.Cpf, row.Name, row.BirthDate),
            cancellationToken);

        cache[key] = identity;

        return identity;
    }

    private static Guid Resolve(IReadOnlyDictionary<int, Guid> map, int key, string description)
        => map.TryGetValue(key, out var id)
            ? id
            : throw new InvalidOperationException($"TSE {description} is not imported.");

    /// <summary>
    /// Resolves a TSE scope to a state row, creating the synthetic national/exterior rows on
    /// demand because IBGE only publishes the 27 states.
    /// </summary>
    private async Task<Guid?> ResolveStateIdAsync(
        string scope,
        IReadOnlyDictionary<string, Guid> stateIds,
        CancellationToken cancellationToken)
    {
        if (stateIds.TryGetValue(scope, out var existing))
            return existing;

        if (!TseScopes.IsSynthetic(scope))
            return null;

        return await syntheticScopes.EnsureAsync(
            scope,
            geography.UpsertStatesAsync,
            cancellationToken);
    }
}
