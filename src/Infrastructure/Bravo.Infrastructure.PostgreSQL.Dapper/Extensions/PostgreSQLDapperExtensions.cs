using Bravo.Application.Acquisition.Providers;
using Bravo.Application.Importing.Contracts;
using Bravo.Domain.Geography.Entities;
using Bravo.Domain.Users.Repositories;
using Bravo.Infrastructure.PostgreSQL.Dapper.Options;
using Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Importing;
using Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Users;
using Bravo.Infrastructure.PostgreSQL.Dapper.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Extensions;

public static class PostgreSQLDapperExtensions
{
    public static IServiceCollection AddPostgreSQLDapper(this IServiceCollection services)
    {
        return services.AddDatabaseOptions()
                       .AddDataSource()
                       .AddScoped<PostgreSQLSession>()
                       .AddQueryRepositories()
                       .AddImportRepositories();
    }

    private static IServiceCollection AddQueryRepositories(this IServiceCollection services)
    {
        return services.AddScoped<IUserQueryRepository, UserQueryRepository>();
    }

    private static IServiceCollection AddImportRepositories(this IServiceCollection services)
    {
        return services.AddScoped<IImportJobRepository, ImportJobRepository>()
                       .AddScoped<IImportWorkUnitRepository, ImportWorkUnitRepository>()
                       .AddScoped<IImportFailedRecordRepository, ImportFailedRecordRepository>()
                       .AddScoped<IImportDatasetRepository, ImportDatasetRepository>()
                       .AddScoped<IDatasetVersionRepository, DatasetVersionRepository>()
                       .AddScoped<IImportBatchStore<State>, StateImportBatchStore>()
                       .AddScoped<IImportBatchStore<IbgeMunicipalityRecord>, MunicipalityImportBatchStore>();
    }

    private static IServiceCollection AddDataSource(this IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<PostgresOptions>>().Value;
            return NpgsqlDataSource.Create(options.Connection);
        });

        return services;
    }

    private static IServiceCollection AddDatabaseOptions(this IServiceCollection services)
    {
        services.AddOptions<PostgresOptions>()
            .BindConfiguration("PostgreSQL")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
