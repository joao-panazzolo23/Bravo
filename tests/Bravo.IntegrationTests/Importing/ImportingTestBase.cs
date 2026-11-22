using Bravo.Application.Imports.Common.Commands;
using Bravo.Application.Imports.Ibge.Commands;
using Bravo.Application.Imports.Tse.Commands;
using Bravo.Application.Imports.Common.ViewModels;
using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Enums;
using Bravo.Domain.Importing.Repositories;
using Bravo.IntegrationTests.Abstract;
using Bravo.IntegrationTests.Configurations;
using Bravo.Shared.Result;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.IntegrationTests.Importing;

/// <summary>
/// Shared helpers for the job-level import tests. There are no work units any more, so every
/// helper works against the job row.
/// </summary>
[Collection("Database")]
public abstract class ImportingTestBase(PostgresContainerFixture pg) : IntegrationTestBase(pg)
{
    protected PostgresContainerFixture Pg { get; } = pg;
    protected FakeImportSource Source => Pg.Factory.Source;
    protected override string[] Schemas => ["public"];

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        Source.Reset();
    }

    protected async Task<Guid> CreateJobAsync(
        string? scope = null,
        int batchSize = 10,
        int maxAttempts = 3,
        int maxParallelism = 2,
        int leaseDurationSeconds = 60,
        string? source = null)
    {
        var job = ImportJob.Create(
            source ?? Source.Key,
            scope ?? FakeImportSource.DefaultScope,
            batchSize,
            maxAttempts,
            maxParallelism,
            leaseDurationSeconds);

        await using var serviceScope = Pg.Factory.Services.CreateAsyncScope();
        var repository = serviceScope.ServiceProvider.GetRequiredService<IImportJobRepository>();

        if (!await repository.CreateAsync(job))
            throw new InvalidOperationException("The test job could not be created.");

        return job.Id;
    }

    protected async Task<bool> TryCreateJobAsync(ImportJob job)
    {
        await using var serviceScope = Pg.Factory.Services.CreateAsyncScope();
        var repository = serviceScope.ServiceProvider.GetRequiredService<IImportJobRepository>();

        return await repository.CreateAsync(job);
    }

    protected async Task<ImportJob?> GetJobAsync(Guid jobId)
    {
        await using var serviceScope = Pg.Factory.Services.CreateAsyncScope();
        var repository = serviceScope.ServiceProvider.GetRequiredService<IImportJobRepository>();

        return await repository.GetAsync(jobId);
    }

    protected async Task<ImportJob?> ClaimAsync(
        Guid jobId,
        string workerId = "worker-test",
        int leaseSeconds = 60)
    {
        await using var serviceScope = Pg.Factory.Services.CreateAsyncScope();
        var repository = serviceScope.ServiceProvider.GetRequiredService<IImportJobRepository>();

        return await repository.ClaimAsync(jobId, workerId, TimeSpan.FromSeconds(leaseSeconds));
    }

    protected async Task<ImportJob?> RunJobAsync(Guid jobId)
    {
        await using var serviceScope = Pg.Factory.Services.CreateAsyncScope();
        var worker = ActivatorUtilities.CreateInstance<TestImportRunner>(
            serviceScope.ServiceProvider);

        return await worker.RunAsync(jobId);
    }

    protected async Task<bool> CancelJobAsync(Guid jobId)
    {
        await using var serviceScope = Pg.Factory.Services.CreateAsyncScope();
        var repository = serviceScope.ServiceProvider.GetRequiredService<IImportJobRepository>();

        return await repository.CancelAsync(jobId);
    }

    protected async Task ReleaseAsync(Guid jobId, string workerId = "worker-test")
    {
        await using var serviceScope = Pg.Factory.Services.CreateAsyncScope();
        var repository = serviceScope.ServiceProvider.GetRequiredService<IImportJobRepository>();

        await repository.ReleaseAsync(jobId, workerId, "test release");
    }

    protected async Task<IReadOnlyList<Guid>> ListRecoverableIdsAsync(int limit = 100)
    {
        await using var serviceScope = Pg.Factory.Services.CreateAsyncScope();
        var repository = serviceScope.ServiceProvider.GetRequiredService<IImportJobRepository>();

        return await repository.ListRecoverableIdsAsync(limit);
    }

    protected async Task<IReadOnlyList<ImportFailedRecord>> GetFailedRecordsAsync(Guid jobId)
    {
        await using var serviceScope = Pg.Factory.Services.CreateAsyncScope();
        var repository = serviceScope.ServiceProvider
            .GetRequiredService<IImportFailedRecordRepository>();

        return await repository.ListByJobAsync(jobId, 100);
    }

    protected async Task<int> CountAsync(string table)
    {
        await using var command = Db.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{table}\";";

        return (int)(long)(await command.ExecuteScalarAsync())!;
    }

    protected async Task<Result<ImportJobCreatedViewModel>> RetryJobAsync(Guid jobId)
    {
        await using var serviceScope = Pg.Factory.Services.CreateAsyncScope();
        var mediator = serviceScope.ServiceProvider.GetRequiredService<IMediator>();

        return await mediator.Send(new RetryImportJobCommand(jobId));
    }

    protected async Task<Result<ImportJobCreatedViewModel>> StartAsync(
        string source,
        string scope)
    {
        await using var serviceScope = Pg.Factory.Services.CreateAsyncScope();
        var mediator = serviceScope.ServiceProvider.GetRequiredService<IMediator>();

        return source switch
        {
            "IBGE" => await mediator.Send(new StartIbgeImportCommand()),
            _ => await mediator.Send(new StartTseImportCommand())
        };
    }

    protected static ImportJob NewJob(
        string source = FakeImportSource.DefaultKey,
        string scope = FakeImportSource.DefaultScope,
        int maxAttempts = 3,
        int maxParallelism = 2,
        int leaseDurationSeconds = 60)
        => ImportJob.Create(source, scope, 10, maxAttempts, maxParallelism, leaseDurationSeconds);

    protected static string UniqueScope(string prefix) => $"{prefix}:{Guid.NewGuid():N}";

    protected static bool IsActive(ImportJob job)
        => job.Status is ImportJobStatus.Pending or ImportJobStatus.Running;
}
