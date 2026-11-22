using Bravo.Application.Imports.Tse.Services;
using Bravo.Application.Tse.Services;
using Bravo.Domain.Importing.Models;
using Bravo.Domain.Importing.Services;

namespace Bravo.Application.Tse;

/// <summary>
/// TSE import facade.
/// </summary>
/// <remarks>
/// This type owns the order of the TSE stages and nothing else. Reference data must be committed
/// before candidates and results, because both resolve against the elections, offices, parties and
/// federations the reference stage writes. The election snapshot is then built once and shared by
/// the two later stages.
///
/// Each stage is an independently understandable service with its own dependencies; the facade
/// injects three of them instead of every repository, reader and resolver the source needs.
/// </remarks>
public sealed class TseImportSource(
    TseReferenceImporter references,
    TseCandidateImporter candidates,
    TseResultImporter results) : IImportSource
{
    public const string SourceKey = "TSE";

    public const string Scope = "TSE:electoral";

    public string Key => SourceKey;

    public async Task ExecuteAsync(
        ImportExecutionContext context,
        CancellationToken cancellationToken)
    {
        var electionLookup = await references.RunAsync(context, cancellationToken);

        await candidates.RunAsync(context, electionLookup, cancellationToken);
        await results.RunAsync(context, electionLookup, cancellationToken);
    }
}
