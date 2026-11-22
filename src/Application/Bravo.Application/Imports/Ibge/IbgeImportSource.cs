using Bravo.Application.Imports.Ibge.Services;
using Bravo.Domain.Importing.Models;
using Bravo.Domain.Importing.Services;

namespace Bravo.Application.Imports.Ibge;

/// <summary>
/// IBGE import facade.
/// </summary>
/// <remarks>
/// The IBGE import is deliberately small: two ordered stages over a bounded set of reference rows.
/// Keeping the facade separate from the importer means the composition point stays stable if the
/// orchestration grows, and the importer can be tested without an <c>IImportSource</c>.
/// </remarks>
public sealed class IbgeImportSource(IbgeReferenceImporter importer) : IImportSource
{
    public const string SourceKey = "IBGE";

    public const string Scope = "IBGE:localities";

    public string Key => SourceKey;

    public async Task ExecuteAsync(
        ImportExecutionContext context,
        CancellationToken cancellationToken)
        => await importer.RunAsync(context, cancellationToken);
}
