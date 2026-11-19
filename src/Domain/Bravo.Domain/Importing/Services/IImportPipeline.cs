using Bravo.Domain.Importing.Entities;

namespace Bravo.Domain.Importing.Services;

public interface IImportPipeline
{
    string UnitKey { get; }

    Task ProcessAsync(
        ImportJob job,
        ImportWorkUnit workUnit,
        CancellationToken cancellationToken = default);
}
