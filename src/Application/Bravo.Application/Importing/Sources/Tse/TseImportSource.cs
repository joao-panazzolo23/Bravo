using Bravo.Application.Electoral.Services;
using Bravo.Application.Importing.Services;
using Bravo.Domain.Electoral.Repositories;
using Bravo.Domain.Electoral.Services;
using Bravo.Domain.Importing.Models;
using Bravo.Domain.Importing.Services;
using Bravo.Domain.Geography.Repositories;
using Microsoft.Extensions.Logging;

namespace Bravo.Application.Importing.Sources.Tse;

///Trash, destroys the simplicity of the project
public sealed partial class TseImportSource(
    ITseDatasetAcquirer acquirer,
    ITseReferenceReader referenceReader,
    ITseCandidateReader candidateReader,
    ITseSectionResultReader sectionReader,
    IElectionRepository elections,
    IElectoralReferenceRepository reference,
    ICoalitionRepository coalitions,
    ICandidateRepository candidates,
    IElectoralStructureRepository structure,
    IElectionResultRepository results,
    IGeographyRepository geography,
    ITseMunicipalityCodeRepository municipalityCodes,
    TseMunicipalityResolver municipalityResolver,
    TseSyntheticScopeSeeder syntheticScopes,
    DatasetVersionService datasetVersions,
    ImportResourceGovernor resources,
    ILogger<TseImportSource> logger) : IImportSource
{
    public const string SourceKey = "TSE";

    public const string Scope = "TSE:electoral";

    public string Key => SourceKey;

    public async Task ExecuteAsync(
        ImportExecutionContext context,
        CancellationToken cancellationToken)
    {
        await ImportReferencesAsync(context, cancellationToken);

        // Elections and rounds are written by the reference stage and are small and bounded, so
        // one immutable snapshot serves every later stage for the whole job execution.
        var electionLookup = await TseElectionLookup.LoadAsync(elections, cancellationToken);

        await ImportCandidatesAsync(context, electionLookup, cancellationToken);
        await ImportResultsAsync(context, electionLookup, cancellationToken);
    }
}
