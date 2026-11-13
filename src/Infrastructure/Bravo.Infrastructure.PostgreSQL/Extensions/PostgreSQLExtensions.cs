using Bravo.Domain.Users.Repositories;
using Bravo.Infrastructure.PostgreSQL.Context;
using Bravo.Infrastructure.PostgreSQL.Data;
using Bravo.Infrastructure.PostgreSQL.Repositories.Users;
using Bravo.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.Infrastructure.PostgreSQL.Extensions;

public static class PostgreSQLExtensions
{
    public static IServiceCollection AddPostgreSQL(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext(configuration)
                .AddRepositories();

        return services;
    }
    public static IServiceCollection AddDbContext(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddDbContext<PostgreSQLContext>(options =>
                    options.UseNpgsql(
                        configuration.GetValue<string>("PostgreSQL:Connection")
                    ))
                .AddScoped<IUnityOfWork, UnityOfWork>();
    }

    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        return services.AddScoped<IUserRepository, UserRepository>()
                       .AddScoped<IUserRecoveryRepository, UserRecoveryRepository>();
    }
    /// <summary>
    /// This method needs to be called AFTER context is registered as a service 
    /// And also AFTER app is Build at Program.cs
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceProvider MigrateDataBase(this IServiceProvider serviceProvider)
    {
        serviceProvider.CreateScope()
            .ServiceProvider
            .GetRequiredService<PostgreSQLContext>()
            .Database
            .Migrate();

        return serviceProvider;
    }
}
