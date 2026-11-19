using Bravo.Domain.Electoral.Services;
using Bravo.Domain.Electoral.Models;
using Bravo.Application.Importing;
using Bravo.Domain.Importing.Services;
using Bravo.Application.Importing.Exceptions;
using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Importing.Entities;
using Microsoft.Extensions.Logging;
using Bravo.Domain.Electoral.Repositories;

namespace Bravo.Application.Electoral.Services;

/// <summary>
/// Imports elections, rounds, offices, parties and federations by reading every state file in
/// the candidate archive for a year.
/// </summary>
/// <remarks>
/// Reference values are denormalised per candidate row, so this stage reads the same archive
/// as the candidates stage. It runs first because candidates need the reference rows.
/// </remarks>
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
        if (!TseImport.TryParseYear(workUnit.UnitKey, TseImport.ReferenceStage, out var year))
            throw new ImportPermanentException(TseImport.DescribeInvalidKey(workUnit.UnitKey));

        var files = await acquirer.AcquireCandidatesAsync(year, cancellationToken);

        if (files.Count == 0)
            throw new ImportPermanentException($"The {year} candidate archive contains no state files.");

        var offices = new Dictionary<int, TseOfficeReference>();
        var parties = new Dictionary<(int, string), TsePartyReference>();
        var federations = new Dictionary<int, TseFederationReference>();
        var seenElections = new HashSet<(int Code, short Round)>();

        foreach (var file in files)
        {
            await using var stream = File.OpenRead(file.Dataset.FilePath);

            var snapshot = reader.Read(stream, file.Dataset.LayoutVersion);

            foreach (var electionRow in snapshot.Elections)
            {
                if (!seenElections.Add((electionRow.Code, electionRow.Round)))
                    continue;

                await UpsertElectionAsync(electionRow, cancellationToken);
            }

            foreach (var office in snapshot.Offices)
                offices[office.Code] = office;

            foreach (var party in snapshot.Parties)
                parties[(party.Number, party.Acronym)] = party;

            foreach (var federation in snapshot.Federations)
                federations[federation.Number] = federation;
        }

        await reference.UpsertOfficesAsync(
            offices.Values.Select(row => ElectoralOffice.Create(row.Code, row.Name)).ToArray(),
            cancellationToken);

        await reference.UpsertPartiesAsync(
            parties.Values.Select(row => PoliticalParty.Create(row.Number, row.Acronym, row.Name)).ToArray(),
            cancellationToken);

        await reference.UpsertFederationsAsync(
            federations.Values
                .Select(row => Federation.Create(row.Number, row.Name, row.Acronym, row.Composition))
                .ToArray(),
            cancellationToken);

        logger.LogInformation(
            "Imported {Year} reference data: {Elections} election rounds, {Offices} offices, {Parties} parties, {Federations} federations.",
            year, seenElections.Count, offices.Count, parties.Count, federations.Count);
    }

    private async Task UpsertElectionAsync(
        TseElectionReference row,
        CancellationToken cancellationToken)
    {
        var election = await elections.UpsertAsync(
            Election.Create(row.Code, row.Year, row.TypeCode, row.TypeName, row.Description, row.Date),
            cancellationToken);

        var roundName = row.Round == 1 ? "1º turno" : $"{row.Round}º turno";

        await elections.UpsertRoundAsync(
            ElectionRound.Create(election.Id, row.Round, roundName),
            cancellationToken);
    }
}
