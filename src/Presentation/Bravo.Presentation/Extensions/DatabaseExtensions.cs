using Bravo.Infrastructure.PostgreSQL.Dapper.Extensions;
using Bravo.Infrastructure.PostgreSQL.Extensions;

namespace Bravo.Presentation.Extensions;

public static class DatabaseExtensions
{
    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration
        )
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        return services.AddPostgreSQLDapper()
                       .AddPostgreSQL(configuration);
    }

    public static IApplicationBuilder UseDatabase(this IApplicationBuilder app)
    {
        ((WebApplication)app).Services.MigrateDataBase();
        return app;
    }
}
