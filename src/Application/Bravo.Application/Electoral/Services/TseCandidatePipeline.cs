using Bravo.Domain.Electoral.Services;
using Bravo.Domain.Electoral.Models;
using Bravo.Application.Importing;
using Bravo.Domain.Importing.Services;
using Bravo.Application.Importing.Exceptions;
using Bravo.Application.Importing.Services;
using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Enums;
using Microsoft.Extensions.Logging;
using Bravo.Domain.Electoral.Repositories;
using Bravo.Domain.Geography.Repositories;

namespace Bravo.Application.Electoral.Services;

/// <summary>
/// Imports every election-specific candidate registration for a year, across every state
/// present in the archive.
/// </summary>
/// <remarks>
/// Permanent identity (CPF-keyed) is separated from the registration row. The round is taken
/// from each row's election code, so a file that contains both rounds is handled correctly.
/// </remarks>
public sealed class TseCandidatePipeline(
    ITseDatasetAcquirer acquirer,
    ITseCandidateReader reader,
    IElectionRepository elections,
    IElectoralReferenceRepository reference,
    ICoalitionRepository coalitions,
    ICandidateRepository candidates,
    IGeographyRepository geography,
    TseSyntheticScopeSeeder syntheticScopes,
    ImportProgressReporterFactory progressFactory,
    ILogger<TseCandidatePipeline> logger
) : IImportPipeline
{
    private const int BatchSize = 5_000;

    public string UnitKey => TseImport.CandidatesStage;

    public async Task ProcessAsync(
        ImportJob job,
        ImportWorkUnit workUnit,
        CancellationToken cancellationToken = default)
    {
        if (!TseImport.TryParseYear(workUnit.UnitKey, TseImport.CandidatesStage, out var year))
            throw new ImportPermanentException(TseImport.DescribeInvalidKey(workUnit.UnitKey));

        using var unitScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["Dataset"] = TseImport.CandidatesStage,
            ["ElectionYear"] = year
        });

        var progress = progressFactory.Create(job, workUnit);

        await progress.StageAsync(ImportStage.Downloading, cancellationToken);
        logger.LogInformation(
            "Downloading candidate dataset. Dataset: {Dataset}, ElectionYear: {Year}.",
            TseImport.CandidatesStage, year);

        var files = await acquirer.AcquireCandidatesAsync(year, cancellationToken);

        if (files.Count == 0)
            throw new ImportPermanentException($"The {year} candidate archive contains no state files.");

        await progress.StageAsync(ImportStage.Reading, cancellationToken);

        var stateIds = await geography.GetStateIdsByAcronymAsync(cancellationToken);
        var officeIds = await reference.GetOfficeIdsAsync(cancellationToken);
        var partyIds = await reference.GetPartyIdsAsync(cancellationToken);
        var federationIds = await reference.GetFederationIdsAsync(cancellationToken);

        var identities = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var total = 0;

        foreach (var file in files)
        {
            var stateId = await ResolveStateIdAsync(file.StateAcronym, stateIds, cancellationToken);

            if (stateId is null)
            {
                logger.LogWarning(
                    "Skipping {Year}/{State}: no geography row could be resolved.",
                    year, file.StateAcronym);

                continue;
            }

            await using var stream = File.OpenRead(file.Dataset.FilePath);

            var rows = reader.Read(stream, file.Dataset.LayoutVersion);

            total += await ImportFileAsync(
                year,
                file.StateAcronym,
                stateId.Value,
                rows,
                officeIds,
                partyIds,
                federationIds,
                identities,
                progress,
                total,
                cancellationToken);
        }

        await progress.FlushAsync(cancellationToken);

        logger.LogInformation(
            "Imported {Count} candidate registrations for {Year} across {States} state files.",
            total, year, files.Count
        );
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
        IImportProgressReporter progress,
        int alreadyImported,
        CancellationToken cancellationToken)
    {
        var imported = 0;
        var byElection = rows
            .GroupBy(row => (row.ElectionCode, row.Round))
            .ToArray();

        foreach (var group in byElection)
        {
            var election = await elections.FindByCodeAsync(group.Key.ElectionCode, cancellationToken);

            if (election is null)
                continue;

            var round = await elections.FindRoundAsync(election.Id, group.Key.Round, cancellationToken);

            if (round is null)
                continue;

            var coalitionIds = await UpsertCoalitionsAsync(
                group.ToArray(), round.Id, stateId, officeIds, cancellationToken);

            // The same candidate sequential can appear more than once in a file (for example
            // when a candidacy has multiple rows); the upsert key is (election, sequential), so
            // duplicates within one command must be collapsed first.
            var buffer = new Dictionary<(string Seq, int Ballot, string Name), Candidate>(BatchSize);

            foreach (var row in group)
            {
                var officeId = Resolve(officeIds, row.OfficeCode, $"office {row.OfficeCode}");

                if (!partyIds.TryGetValue((row.PartyNumber, row.PartyAcronym.ToUpperInvariant()), out var partyId))
                    throw new ImportPermanentException(
                        $"Party {row.PartyNumber}/{row.PartyAcronym} is not imported.");

                Guid? federationId = null;

                if (row.FederationNumber is > 0 and var federationNumber
                    && federationIds.TryGetValue(federationNumber, out var resolvedFederation))
                {
                    federationId = resolvedFederation;
                }

                Guid? coalitionId = null;

                if (row.HasCoalition
                    && row.CoalitionNumber is { } coalitionNumber
                    && coalitionIds.TryGetValue((round.Id, officeId, coalitionNumber), out var resolvedCoalition))
                {
                    coalitionId = resolvedCoalition;
                }

                var identityId = await ResolveIdentityAsync(row, identities, cancellationToken);

                var candidate = Candidate.Create(
                    election.Id,
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

                buffer[(row.Sequential, row.BallotNumber, row.Name)] = candidate;

                if (buffer.Count >= BatchSize)
                {
                    await progress.StageAsync(ImportStage.Persisting, cancellationToken);

                    await candidates.UpsertCandidatesAsync(buffer.Values.ToArray(), cancellationToken);

                    imported += buffer.Count;

                    await progress.RecordsAsync(
                        alreadyImported + imported,
                        alreadyImported + imported,
                        0,
                        0,
                        cancellationToken: cancellationToken);

                    buffer.Clear();
                }
            }

            if (buffer.Count > 0)
            {
                await candidates.UpsertCandidatesAsync(buffer.Values.ToArray(), cancellationToken);
                imported += buffer.Count;
            }
        }

        return imported;
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

        var entities = new List<Coalition>(distinct.Length);

        foreach (var row in distinct)
        {
            var officeId = Resolve(officeIds, row.OfficeCode, $"office {row.OfficeCode}");

            entities.Add(Coalition.Create(
                roundId,
                officeId,
                stateId,
                row.CoalitionNumber!.Value,
                row.CoalitionName ?? $"Coalition {row.CoalitionNumber}",
                row.CoalitionComposition));
        }

        await coalitions.UpsertAsync(entities, cancellationToken);

        return await coalitions.GetIdsAsync(cancellationToken);
    }

    private async Task<Guid?> ResolveIdentityAsync(
        TseCandidateRow row,
        Dictionary<string, Guid> cache,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(row.Cpf))
            return null;

        if (cache.TryGetValue(row.Cpf, out var cached))
            return cached;

        var identity = await candidates.UpsertIdentityAsync(
            CandidateIdentity.Create(row.Cpf, row.Name, row.BirthDate),
            cancellationToken);

        cache[row.Cpf] = identity;

        return identity;
    }

    private static Guid Resolve(IReadOnlyDictionary<int, Guid> map, int key, string description)
        => map.TryGetValue(key, out var id)
            ? id
            : throw new ImportPermanentException($"{description} is not imported.");

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
