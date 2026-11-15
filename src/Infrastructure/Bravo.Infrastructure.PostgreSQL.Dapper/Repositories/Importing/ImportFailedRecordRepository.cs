using Bravo.Application.Importing.Contracts;
using Bravo.Domain.Importing.Entities;
using Dapper;
using Npgsql;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Importing;

internal sealed class ImportFailedRecordRepository(NpgsqlDataSource dataSource) : IImportFailedRecordRepository
{
    private const string SelectByJobSql = """
        SELECT *
        FROM "ImportFailedRecords"
        WHERE "JobId" = @JobId
        ORDER BY "CreatedAt" DESC, "Id"
        LIMIT @Limit;
        """;

    public async Task<IReadOnlyList<ImportFailedRecord>> ListByJobAsync(
        Guid jobId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var records = await connection.QueryAsync<ImportFailedRecord>(new CommandDefinition(
            SelectByJobSql,
            new { JobId = jobId, Limit = limit },
            cancellationToken: cancellationToken));

        return records.AsList();
    }
}
