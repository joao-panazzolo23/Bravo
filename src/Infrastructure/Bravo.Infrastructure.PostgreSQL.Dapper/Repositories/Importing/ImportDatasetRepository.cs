using Bravo.Application.Importing.Contracts;
using Bravo.Domain.Importing.Entities;
using Dapper;
using Npgsql;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Importing;

internal sealed class ImportDatasetRepository(NpgsqlDataSource dataSource) : IImportDatasetRepository
{
    private const string InsertSql = """
        INSERT INTO "ImportDatasets"
            ("Id", "Provider", "Dataset", "ElectionYear", "LayoutVersion", "SourceUrl",
             "SourceFileName", "Checksum", "SizeBytes", "AcquiredAt", "CreatedAt", "UpdatedAt")
        VALUES
            (@Id, @Provider, @Dataset, @ElectionYear, @LayoutVersion, @SourceUrl,
             @SourceFileName, @Checksum, @SizeBytes, @AcquiredAt, @CreatedAt, @UpdatedAt)
        ON CONFLICT ("Provider", "Dataset", "ElectionYear", "Checksum")
        DO UPDATE SET "UpdatedAt" = EXCLUDED."UpdatedAt"
        RETURNING *;
        """;

    private const string SelectByChecksumSql = """
        SELECT *
        FROM "ImportDatasets"
        WHERE "Provider" = @Provider
          AND "Dataset" = @Dataset
          AND "Checksum" = @Checksum
          AND ("ElectionYear" = @ElectionYear OR ("ElectionYear" IS NULL AND @ElectionYear IS NULL));
        """;

    public async Task<ImportDataset> RegisterAsync(
        ImportDataset dataset,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        return await connection.QuerySingleAsync<ImportDataset>(new CommandDefinition(
            InsertSql,
            new
            {
                dataset.Id,
                dataset.Provider,
                dataset.Dataset,
                dataset.ElectionYear,
                dataset.LayoutVersion,
                dataset.SourceUrl,
                dataset.SourceFileName,
                dataset.Checksum,
                dataset.SizeBytes,
                dataset.AcquiredAt,
                dataset.CreatedAt,
                dataset.UpdatedAt
            },
            cancellationToken: cancellationToken));
    }

    public async Task<ImportDataset?> FindByChecksumAsync(
        string provider,
        string dataset,
        int? electionYear,
        string checksum,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<ImportDataset>(new CommandDefinition(
            SelectByChecksumSql,
            new { Provider = provider, Dataset = dataset, ElectionYear = electionYear, Checksum = checksum },
            cancellationToken: cancellationToken));
    }
}
