using Bravo.Application.Electoral.Contracts;
using Bravo.Application.Electoral.Models;
using Bravo.Application.Importing;
using Bravo.Application.Importing.Contracts;
using Bravo.Application.Importing.Exceptions;
using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Importing.Entities;
using Microsoft.Extensions.Logging;

namespace Bravo.Application.Electoral.Services;

/// <summary>
/// Imports election-specific candidate registrations (candidacies), keeping permanent
/// identity (CPF-keyed) separate from the registration row.
/// </summary>
public sealed class TseCandidatePipeline(
    ITseDatasetAcquirer acquirer,
    ITseCandidateReader reader,
    IElectionRepository elections,
    IElectoralReferenceRepository reference,
    ICoalitionRepository coalitions,
    ICandidateRepository candidates,
    IGeographyLookup geography,
    ILogger<TseCandidatePipeline> logger) : IImportPipeline
{
    public string UnitKey => TseImport.CandidatesStage;

    public async Task ProcessAsync(
        ImportJob job,
        ImportWorkUnit workUnit,
        CancellationToken cancellationToken = default)
    {
        var scope = TseImportScope.Parse(workUnit.UnitKey);

        var dataset = await acquirer.AcquireCandidatesAsync(scope, cancellationToken);

        await using var stream = File.OpenRead(dataset.FilePath);

        var rows = reader.Read(stream, dataset.LayoutVersion);

        if (rows.Count == 0)
            return;

        var election = await elections.FindByCodeAsync(scope.ElectionCode, cancellationToken)
            ?? throw new ImportPermanentException(
                $"Election {scope.ElectionCode} is not imported; run the reference stage first.");

        var round = await elections.FindRoundAsync(election.Id, scope.Round, cancellationToken)
            ?? throw new ImportPermanentException(
                $"Round {scope.Round} for election {scope.Year} is not imported; run the reference stage first.");

        var stateIds = await geography.GetStateIdsByAcronymAsync(cancellationToken);

        if (!stateIds.TryGetValue(scope.State, out var stateId))
            throw new ImportPermanentException(
                $"State '{scope.State}' is not present; import IBGE states first.");

        var officeIds = await reference.GetOfficeIdsAsync(cancellationToken);
        var partyIds = await reference.GetPartyIdsAsync(cancellationToken);
        var federationIds = await reference.GetFederationIdsAsync(cancellationToken);

        var importedCoalitions = await UpsertCoalitionsAsync(rows, round, stateId, officeIds, cancellationToken);
        var coalitionIds = importedCoalitions.Count == 0
            ? await coalitions.GetIdsAsync(cancellationToken)
            : importedCoalitions;

        var identities = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var registrations = new List<Candidate>(rows.Count);

        foreach (var row in rows)
        {
            var officeId = Resolve(officeIds, row.OfficeCode, $"office {row.OfficeCode}");

            var partyKey = (row.PartyNumber, row.PartyAcronym);

            if (!partyIds.TryGetValue(partyKey, out var partyId))
                throw new ImportPermanentException($"Party {row.PartyNumber}/{row.PartyAcronym} is not imported.");

            Guid? federationId = null;

            if (row.FederationNumber is > 0 and var federationNumber)
            {
                if (federationIds.TryGetValue(federationNumber, out var resolved))
                    federationId = resolved;
            }

            Guid? coalitionId = null;

            if (row.HasCoalition
                && row.CoalitionNumber is { } coalitionNumber
                && coalitionIds.TryGetValue((round.Id, officeId, coalitionNumber), out var resolvedCoalition))
            {
                coalitionId = resolvedCoalition;
            }

            var identityId = await ResolveIdentityAsync(row, identities, cancellationToken);

            registrations.Add(Candidate.Create(
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
                row.BirthDate));
        }

        await candidates.UpsertCandidatesAsync(registrations, cancellationToken);

        logger.LogInformation(
            "Imported {Count} candidate registrations for {Year}/{State} round {Round}.",
            registrations.Count,
            scope.Year,
            scope.State,
            scope.Round);
    }

    private async Task<IReadOnlyDictionary<(Guid, Guid, long), Guid>> UpsertCoalitionsAsync(
        IReadOnlyList<TseCandidateRow> rows,
        ElectionRound round,
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
                round.Id,
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
}
