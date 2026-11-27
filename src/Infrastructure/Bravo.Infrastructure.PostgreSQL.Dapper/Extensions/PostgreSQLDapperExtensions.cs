using Bravo.Domain.Importing.Services;
using Bravo.Domain.Geography.Entities;
using Bravo.Domain.Users.Repositories;
using Bravo.Domain.Electoral.Analytics;
using Bravo.Infrastructure.PostgreSQL.Dapper.Options;
using Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Analytics;
using Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Importing;
using Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Users;
using Bravo.Infrastructure.PostgreSQL.Dapper.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Bravo.Domain.Importing.Repositories;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Extensions;

public static class PostgreSQLDapperExtensions
{
    public static IServiceCollection AddPostgreSQLDapper(this IServiceCollection services)
    {
        TypeHandlers.DapperTypeHandlers.Register();

        return services.AddDatabaseOptions()
                       .AddDataSource()
                       .AddScoped<PostgreSQLSession>()
                       .AddQueryRepositories()
                       .AddImportRepositories();
    }

    private static IServiceCollection AddQueryRepositories(this IServiceCollection services)
    {
        return services.AddScoped<IUserQueryRepository, UserQueryRepository>()
                       .AddAnalyticsRepositories();
    }

    private static IServiceCollection AddAnalyticsRepositories(this IServiceCollection services)
    {
        return services.AddScoped<ElectionDataAnalytics>()
                       .AddScoped<VoteConcentrationQueries>()
                       .AddScoped<MunicipalityPerformanceQueries>()
                       .AddScoped<MunicipalityMarginQueries>()
                       .AddScoped<TurnoutCompositionQueries>()
                       .AddScoped<SectionOutlierQueries>()
                       .AddScoped<RegionalDominanceQueries>()
                       .AddScoped<ElectionPerformanceQueries>()
                       .AddScoped<IElectionDataAnalytics>(provider =>
                           provider.GetRequiredService<ElectionDataAnalytics>());
    }

    private static IServiceCollection AddImportRepositories(this IServiceCollection services)
    {
        return services.AddScoped<IImportJobRepository, ImportJobRepository>()
                       .AddScoped<IImportFailedRecordRepository, ImportFailedRecordRepository>()
                       .AddScoped<IImportDatasetRepository, ImportDatasetRepository>()
                       .AddScoped<IDatasetVersionRepository, DatasetVersionRepository>()
                       .AddElectoralRepositories();
    }

    private static IServiceCollection AddDataSource(this IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<PostgresOptions>>().Value;

            var builder = new NpgsqlDataSourceBuilder(options.Connection);
            builder.EnableUnmappedTypes();

            return builder.Build();
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
