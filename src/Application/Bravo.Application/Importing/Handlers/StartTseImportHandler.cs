using Bravo.Application.Importing.Commands;
using Bravo.Application.Importing.Services;
using Bravo.Application.Importing.ViewModels;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Importing.Handlers;

public sealed class StartTseImportHandler(ImportJobScheduler scheduler)
    : ICommandHandler<StartTseImportCommand, Result<ImportJobCreatedViewModel>>
{
    /// <summary>
    /// Every state TSE publishes results for, plus the exterior scope ("ZZ") that exists from
    /// 2026. States absent from a given year's archive return 404 and are recorded as a
    /// dead letter rather than failing the whole job.
    /// </summary>
    private static readonly string[] States =
    [
        "AC", "AL", "AM", "AP", "BA", "CE", "DF", "ES", "GO", "MA", "MG", "MS", "MT",
        "PA", "PB", "PE", "PI", "PR", "RJ", "RN", "RO", "RR", "RS", "SC", "SE", "SP", "TO",
        "ZZ"
    ];

    public ValueTask<Result<ImportJobCreatedViewModel>> Handle(
        StartTseImportCommand command,
        CancellationToken cancellationToken)
        => new(scheduler.ScheduleAsync(
            BuildUnitKeys(),
            command.ToRequest(),
            cancellationToken));

    /// <summary>
    /// Units are ordered so a job imports reference data before candidates for each year, and
    /// results only after every candidate archive has been read.
    /// </summary>
    private static string[] BuildUnitKeys()
    {
        var keys = new List<string>();

        foreach (var year in TseYears.Candidates)
        {
            keys.Add(TseImport.ReferenceKey(year));
            keys.Add(TseImport.CandidatesKey(year));
        }

        foreach (var year in TseYears.Results)
        {
            foreach (var state in States)
                keys.Add(TseImport.ResultsKey(year, state));
        }

        return keys.ToArray();
    }
}
