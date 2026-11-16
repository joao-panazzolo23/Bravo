using Bravo.Application.Electoral.Contracts;
using Bravo.Application.Importing;
using Bravo.Application.Importing.Contracts;
using Bravo.Application.Importing.Exceptions;
using Bravo.Domain.Importing.Entities;
using Microsoft.Extensions.Logging;

namespace Bravo.Application.Electoral.Services;

/// <summary>
/// Imports election, round, office, party and federation reference data from the TSE
/// candidate archive. Reference values are denormalised on every candidate row, so this
/// stage reads the candidate file and must run before the candidates stage.
/// </summary>
public sealed class TseReferencePipeline(
    ITseDatasetAcquirer acquirer,
    ITseReferenceReader reader,
    IElectionRepository elections,
    IElectoralReferenceRepository reference,
    ILogger<TseReferencePipeline> logger) : IImportPipeline
{
    public string UnitKey => TseImport.ReferenceStage;

    public async Task ProcessAsync(
        ImportJob job,
        ImportWorkUnit workUnit,
        CancellationToken cancellationToken = default)
    {
        var scope = TseImportScope.Parse(workUnit.UnitKey);

        var dataset = await acquirer.AcquireCandidatesAsync(scope, cancellationToken);

        await using var stream = File.OpenRead(dataset.FilePath);

        var snapshot = reader.Read(stream, dataset.LayoutVersion);

        if (snapshot.Election is null)
            throw new ImportPermanentException("The TSE candidate file contains no election rows.");

        var election = await elections.UpsertAsync(
            Domain.Electoral.Entities.Election.Create(
                snapshot.Election.Code,
                snapshot.Election.Year,
                snapshot.Election.TypeCode,
                snapshot.Election.TypeName,
                snapshot.Election.Description,
                snapshot.Election.Date),
            cancellationToken);

        var roundName = scope.Round == 1 ? "1º turno" : $"{scope.Round}º turno";

        await elections.UpsertRoundAsync(
            Domain.Electoral.Entities.ElectionRound.Create(election.Id, scope.Round, roundName),
            cancellationToken);

        await reference.UpsertOfficesAsync(
            snapshot.Offices
                .Select(row => Domain.Electoral.Entities.ElectoralOffice.Create(row.Code, row.Name))
                .ToArray(),
            cancellationToken);

        await reference.UpsertPartiesAsync(
            snapshot.Parties
                .Select(row => Domain.Electoral.Entities.PoliticalParty.Create(row.Number, row.Acronym, row.Name))
                .ToArray(),
            cancellationToken);

        await reference.UpsertFederationsAsync(
            snapshot.Federations
                .Select(row => Domain.Electoral.Entities.Federation.Create(row.Number, row.Name, row.Acronym, row.Composition))
                .ToArray(),
            cancellationToken);

        logger.LogInformation(
            "Imported TSE reference data for {Year}/{State} round {Round}: {Offices} offices, {Parties} parties, {Federations} federations.",
            scope.Year,
            scope.State,
            scope.Round,
            snapshot.Offices.Count,
            snapshot.Parties.Count,
            snapshot.Federations.Count);
    }
}
