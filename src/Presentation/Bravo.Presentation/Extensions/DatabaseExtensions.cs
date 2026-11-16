using Bravo.Infrastructure.Acquisition.Extensions;
using Bravo.Infrastructure.PostgreSQL.Dapper.Extensions;
using Bravo.Infrastructure.PostgreSQL.Extensions;
using Bravo.Infrastructure.Tse.Extensions;

namespace Bravo.Presentation.Extensions;

public static class DatabaseExtensions
{
    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration
        )
    {
        return services.AddPostgreSQLDapper()
                       .AddPostgreSQL(configuration)
                       .AddAcquisition(configuration)
                       .AddTse(configuration);
    }

    public static IApplicationBuilder UseDatabase(this IApplicationBuilder app)
    {
        ((WebApplication)app).Services.MigrateDataBase();
        return app;
    }
}
