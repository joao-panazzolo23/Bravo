using Bravo.Domain.Importing.Entities;

namespace Bravo.Application.Importing.Contracts;

public interface IImportPipeline
{
    string UnitKey { get; }

    Task ProcessAsync(
        ImportJob job,
        ImportWorkUnit workUnit,
        CancellationToken cancellationToken = default);
}
