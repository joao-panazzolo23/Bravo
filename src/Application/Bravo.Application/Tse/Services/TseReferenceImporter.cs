using Bravo.Application.Imports.Common.Execution;
using Bravo.Application.Logging;
using Bravo.Application.Tse.Models;
using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Models;
using Bravo.Domain.Electoral.Repositories;
using Bravo.Domain.Electoral.Services;
using Bravo.Domain.Importing.Enums;
using Bravo.Domain.Importing.Models;
using Microsoft.Extensions.Logging;

namespace Bravo.Application.Imports.Tse.Services;

/// <summary>
/// Imports the electoral reference data that candidates and results depend on.
/// </summary>
/// <remarks>
/// One year at a time in parallel. A year's candidate archive contains every state file, and each
/// file contributes offices, parties, federations and elections, so the stage reads the whole
/// archive and collapses the observations into one distinct set per year before writing.
///
/// The extracted files stay in the <see cref="TseImportSession"/>, which is what lets
/// <see cref="TseCandidateImporter"/> reuse them instead of downloading the same archive again.
///
/// The result is the election snapshot every later stage needs. Building it here, after the
/// reference writes have committed, is what makes the snapshot complete and immutable.
/// </remarks>
public sealed class TseReferenceImporter(
    ITseDatasetAcquirer acquirer,
    ITseReferenceReader referenceReader,
    IElectionRepository elections,
    IElectoralReferenceRepository reference,
    TseImportSession session,
    ILogger<TseReferenceImporter> logger)
{
    public async Task<TseElectionLookup> RunAsync(
        ImportExecutionContext context,
        CancellationToken cancellationToken)
    {
        var years = TseCatalog.CandidateYears;
        var completed = 0;
        var failed = 0;
        long processed = 0;

        context.Report(
            ImportStage.References,
            years.Length,
            completed,
            failed,
            processed,
            0,
            "Reference data");

        await Parallel.ForEachAsync(
            years,
            ParallelOptionsFor(context, cancellationToken),
            async (year, token) =>
            {
                using var yearScope = logger.BeginOperationScope(year: year);

                try
                {
                    var count = await ImportYearAsync(year, token);

                    Interlocked.Add(ref processed, count);
                    Interlocked.Increment(ref completed);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    Interlocked.Increment(ref failed);

                    ProcessingLog.ReferenceImportFailed(logger, year, exception);
                }

                context.Report(
                    ImportStage.References,
                    years.Length,
                    Volatile.Read(ref completed),
                    Volatile.Read(ref failed),
                    Volatile.Read(ref processed),
                    0,
                    $"Reference {year}");
            });

        // Elections and rounds were written above and are small and bounded, so one immutable
        // snapshot serves every later stage for the whole job execution.
        return await TseElectionLookup.LoadAsync(elections, cancellationToken);
    }

    private async Task<int> ImportYearAsync(int year, CancellationToken cancellationToken)
    {
        var files = await session.GetOrAcquireCandidatesAsync(year, acquirer, cancellationToken);

        if (files.Count == 0)
        {
            throw new InvalidOperationException(
                $"The {year} TSE candidate archive contains no state files.");
        }

        var offices = new Dictionary<int, TseOfficeReference>();
        var parties = new Dictionary<(int, string), TsePartyReference>();
        var federations = new Dictionary<int, TseFederationReference>();
        var electionsSeen = new HashSet<(int Code, short Round)>();

        foreach (var file in files)
        {
            await using var stream = File.OpenRead(file.Dataset.FilePath);
            var snapshot = referenceReader.Read(stream, file.Dataset.LayoutVersion);

            foreach (var election in snapshot.Elections)
            {
                if (!electionsSeen.Add((election.Code, election.Round)))
                    continue;

                await UpsertElectionAsync(election, cancellationToken);
            }

            foreach (var office in snapshot.Offices)
                offices[office.Code] = office;

            foreach (var party in snapshot.Parties)
                parties[(party.Number, party.Acronym.ToUpperInvariant())] = party;

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
            federations.Values.Select(row =>
                Federation.Create(row.Number, row.Name, row.Acronym, row.Composition)).ToArray(),
            cancellationToken);

        PersistenceLog.ReferenceDataUpserted(
            logger, year, electionsSeen.Count, offices.Count, parties.Count, federations.Count);

        return offices.Count + parties.Count + federations.Count;
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

    private static ParallelOptions ParallelOptionsFor(
        ImportExecutionContext context,
        CancellationToken cancellationToken) => new()
        {
            MaxDegreeOfParallelism = Math.Max(1, context.MaxParallelism),
            CancellationToken = cancellationToken
        };
}
