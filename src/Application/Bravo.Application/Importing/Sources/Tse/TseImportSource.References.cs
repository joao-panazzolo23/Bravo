using Bravo.Application.Logging;
using Bravo.Domain.Importing.Enums;
using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Models;
using Bravo.Domain.Importing.Models;
using Microsoft.Extensions.Logging;

namespace Bravo.Application.Importing.Sources.Tse;

public sealed partial class TseImportSource
{
    /// <summary>
    /// ta feio essa bomba
    /// </remarks>
    private async Task ImportReferencesAsync(
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
            "Reference data"
        );

        await Parallel.ForEachAsync(
            years,
            ParallelOptionsFor(context, cancellationToken),
            async (year, token) =>
            {
                using var yearScope = logger.BeginOperationScope(year: year);

                try
                {
                    var count = await ImportReferenceYearAsync(year, token);

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
                    $"Reference {year}"
                );
            });
    }

    private async Task<int> ImportReferenceYearAsync(
        int year,
        CancellationToken cancellationToken)
    {
        var files = await acquirer.AcquireCandidatesAsync(year, cancellationToken);

        if (files.Count == 0)
            throw new InvalidOperationException(
                $"The {year} TSE candidate archive contains no state files.");

        var offices = new Dictionary<int, TseOfficeReference>();
        var parties = new Dictionary<(int, string), TsePartyReference>();
        var federations = new Dictionary<int, TseFederationReference>();
        var elections = new HashSet<(int Code, short Round)>();

        foreach (var file in files)
        {
            await using var stream = File.OpenRead(file.Dataset.FilePath);
            var snapshot = referenceReader.Read(stream, file.Dataset.LayoutVersion);

            foreach (var election in snapshot.Elections)
            {
                if (!elections.Add((election.Code, election.Round)))
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
            cancellationToken
        );

        PersistenceLog.ReferenceDataUpserted(
            logger, year, elections.Count, offices.Count, parties.Count, federations.Count);

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
