using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Enums;
using Bravo.Domain.Importing.Models;
using Bravo.Domain.Importing.Repositories;
using Npgsql;
using Bravo.IntegrationTests.Configurations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.IntegrationTests.Importing;

/// <summary>
/// Binary COPY coverage for the dead-letter table and the reference upserts that previously used
/// <c>jsonb_to_recordset</c>.
/// </summary>
[Collection("Database")]
public sealed class FailedRecordBulkUpsertTests(PostgresContainerFixture pg) : BulkPersistenceTestBase(pg)
{
    [Fact]
    public async Task FailedRecordsAreInsertedWithTypedColumnsAndNullPayload()
    {
        var jobId = await CreateJobAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<IImportFailedRecordRepository>(scope.ServiceProvider);

        await repository.UpsertAsync(jobId, [
            new ImportRejectedRecord("src:1", "{\"raw\":true}", ImportErrorCategory.Deserialization, "Bad JSON."),
            new ImportRejectedRecord("src:2", null, ImportErrorCategory.Validation, "Missing field.")
        ]);

        var records = await repository.ListByJobAsync(jobId, 10);

        records.Should().HaveCount(2);
        records.Should().ContainSingle(record => record.SourceIdentifier == "src:1" && record.Payload == "{\"raw\":true}");
        records.Should().ContainSingle(record => record.SourceIdentifier == "src:2" && record.Payload == null);
        records.Should().OnlyContain(record => record.AttemptCount == 1);
    }

    [Fact]
    public async Task RepeatingTheSameFailureIncrementsTheAttemptCount()
    {
        var jobId = await CreateJobAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<IImportFailedRecordRepository>(scope.ServiceProvider);

        await repository.UpsertAsync(jobId, [
            new ImportRejectedRecord("src:retry", null, ImportErrorCategory.Source, "First.")
        ]);

        await repository.UpsertAsync(jobId, [
            new ImportRejectedRecord("src:retry", null, ImportErrorCategory.Source, "Second.")
        ]);

        var record = (await repository.ListByJobAsync(jobId, 10)).Single();

        record.AttemptCount.Should().Be(2);
        record.ErrorDescription.Should().Be("Second.");
    }

    [Fact]
    public async Task DuplicateSourceIdentifiersInOneBatchCollapseToOneRow()
    {
        var jobId = await CreateJobAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<IImportFailedRecordRepository>(scope.ServiceProvider);

        await repository.UpsertAsync(jobId, [
            new ImportRejectedRecord("src:dup", null, ImportErrorCategory.Source, "First."),
            new ImportRejectedRecord("src:dup", null, ImportErrorCategory.Source, "Last.")
        ]);

        var record = (await repository.ListByJobAsync(jobId, 10)).Single();

        record.ErrorDescription.Should().Be("Last.");
        record.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task ForeignKeyToTheJobStillApplies()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<IImportFailedRecordRepository>(scope.ServiceProvider);

        var action = async () => await repository.UpsertAsync(Guid.CreateVersion7(), [
            new ImportRejectedRecord("src:orphan", null, ImportErrorCategory.Source, "Orphan.")
        ]);

        await action.Should().ThrowAsync<Npgsql.PostgresException>()
            .Where(exception => exception.SqlState == PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task EmptyBatchDoesNotWrite()
    {
        var jobId = await CreateJobAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<IImportFailedRecordRepository>(scope.ServiceProvider);

        await repository.UpsertAsync(jobId, []);

        (await repository.ListByJobAsync(jobId, 10)).Should().BeEmpty();
    }
}
