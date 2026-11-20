using Bravo.Domain.Importing.Models;

namespace Bravo.Domain.Importing.Services;

/// <summary>
/// One importable dataset provider, such as TSE or IBGE.
/// </summary>
/// <remarks>
/// A source owns its own stage orchestration and parallelism; the worker only resolves it by
/// <see cref="Key"/> and runs it. Adding a provider therefore never requires changing the
/// worker.
/// </remarks>
public interface IImportSource
{
    /// <summary>Stable identifier persisted on the job, for example <c>TSE</c>.</summary>
    string Key { get; }

    Task ExecuteAsync(ImportExecutionContext context, CancellationToken cancellationToken);
}
