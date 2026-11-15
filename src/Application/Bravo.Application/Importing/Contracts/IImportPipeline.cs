using Bravo.Domain.Importing.Entities;

namespace Bravo.Application.Importing.Contracts;

public interface IImportPipeline
{
    string ImportType { get; }

    Task ProcessAsync(
        ImportJob job,
        ImportWorkUnit workUnit,
        CancellationToken cancellationToken = default);
}
