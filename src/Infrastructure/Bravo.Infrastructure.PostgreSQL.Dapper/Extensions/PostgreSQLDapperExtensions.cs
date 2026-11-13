using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Bravo.Infrastructure.PostgreSQL.Dapper.Sessions;
using Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Users;
using Bravo.Domain.Users.Repositories;
using Bravo.Infrastructure.PostgreSQL.Dapper.Options;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Extensions;

public static class PostgreSQLDapperExtensions
{
    public static IServiceCollection AddPostgreSQLDapper(this IServiceCollection services)
    {
        return services.AddDatabaseOptions()
                       .AddDataSource()
                       .AddScoped<PostgreSQLSession>()
                       .AddQueryRepositories();
    }

    private static IServiceCollection AddQueryRepositories(this IServiceCollection services)
    {
        return services.AddScoped<IUserQueryRepository, UserQueryRepository>();
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
