using Bravo.Application.Importing.Commands;
using Bravo.Application.Importing.Contracts;
using Bravo.Application.Importing.Models;
using Bravo.Application.Importing.Services;
using Bravo.Application.Importing.ViewModels;
using Bravo.Domain.Geography.Entities;
using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Enums;
using Bravo.IntegrationTests.Abstract;
using Bravo.IntegrationTests.Configurations;
using Bravo.Shared.Result;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.IntegrationTests.Importing;

[Collection("Database")]
public abstract class ImportingTestBase(PostgresContainerFixture pg) : IntegrationTestBase(pg)
{
    protected PostgresContainerFixture Pg { get; } = pg;
    protected TestStateSource Source => Pg.Factory.StateSource;
    protected TestStateValidator Validator => Pg.Factory.StateValidator;
    protected override string[] Schemas => ["public"];

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        Source.Reset();
        Validator.Reset();
    }

    protected async Task<ImportJob> CreateJobAsync(
        string[]? unitKeys = null,
        int batchSize = 2,
        int maxAttempts = 3,
        int leaseDurationSeconds = 60)
    {
        var job = ImportJob.Create(
            batchSize,
            maxAttempts,
            leaseDurationSeconds);

        var keys = unitKeys ?? ["default"];
        var units = keys.Select(key => ImportWorkUnit.Create(job.Id, key)).ToArray();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IImportJobRepository>();
        await repository.CreateAsync(job, units);

        return job;
    }

    protected async Task<bool> TryCreateJobAsync(ImportJob job, params string[] unitKeys)
    {
        var units = unitKeys.Select(key => ImportWorkUnit.Create(job.Id, key)).ToArray();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IImportJobRepository>();
        return await repository.CreateAsync(job, units);
    }

    protected async Task RunJobAsync(Guid jobId, string workerId = "worker-test")
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<ImportJobExecutor>();
        await executor.ExecuteAsync(jobId, workerId);
    }

    protected async Task<ImportJob?> GetJobAsync(Guid jobId)
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IImportJobRepository>();
        return await repository.GetAsync(jobId);
    }

    protected async Task<IReadOnlyList<ImportWorkUnit>> GetWorkUnitsAsync(Guid jobId)
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IImportWorkUnitRepository>();
        return await repository.GetByJobAsync(jobId);
    }

    protected async Task<IReadOnlyList<ImportFailedRecord>> GetFailedRecordsAsync(Guid jobId)
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IImportFailedRecordRepository>();
        return await repository.ListByJobAsync(jobId, 100);
    }

    protected async Task MarkJobRunningAsync(Guid jobId)
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IImportJobRepository>();
        await repository.MarkRunningAsync(jobId);
    }

    protected async Task<bool> CancelJobAsync(Guid jobId)
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IImportJobRepository>();
        return await repository.CancelAsync(jobId);
    }

    protected async Task<Result<ImportJobCreatedViewModel>> RetryJobAsync(Guid jobId)
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(new RetryImportJobCommand(jobId));
    }

    protected async Task<IReadOnlyList<Guid>> ListRecoverableIdsAsync(int limit = 100)
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IImportJobRepository>();
        return await repository.ListRecoverableIdsAsync(limit);
    }

    protected async Task<ImportWorkUnit?> ClaimNextAsync(
        Guid jobId,
        string workerId,
        int leaseSeconds = 60)
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IImportWorkUnitRepository>();
        return await repository.ClaimNextAsync(jobId, workerId, TimeSpan.FromSeconds(leaseSeconds));
    }

    protected async Task ApplyBatchAsync(
        ImportWorkUnit workUnit,
        IReadOnlyList<State> records,
        string? nextCursor,
        bool isFinalBatch,
        int leaseSeconds = 60)
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IImportBatchStore<State>>();

        await store.PersistBatchAsync(
            new ImportBatchWriteContext(
                workUnit.JobId,
                workUnit.Id,
                workUnit.FencingToken,
                workUnit.LeaseOwner!,
                nextCursor,
                isFinalBatch,
                leaseSeconds),
            records,
            []);
    }

    protected async Task<long> CountStatesAsync()
    {
        await using var command = Db.CreateCommand();
        command.CommandText = """SELECT COUNT(*) FROM "States";""";
        return (long)(await command.ExecuteScalarAsync())!;
    }

    protected async Task<string?> GetStateNameAsync(int ibgeCode)
    {
        await using var command = Db.CreateCommand();
        command.CommandText = """SELECT "Name" FROM "States" WHERE "IbgeCode" = @code;""";
        command.Parameters.AddWithValue("code", ibgeCode);
        return (string?)await command.ExecuteScalarAsync();
    }

    protected static State CreateState(int ibgeCode, string? abbreviation = null, string? name = null)
    {
        abbreviation ??= $"{(char)('A' + ibgeCode / 26 % 26)}{(char)('A' + ibgeCode % 26)}";

        var state = State.Create(ibgeCode, abbreviation, name ?? $"State {ibgeCode}", "Sudeste");
        var now = DateTime.UtcNow;
        state.SetCreatedAt(now);
        state.SetUpdatedAt(now);
        return state;
    }
}
