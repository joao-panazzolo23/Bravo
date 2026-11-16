using Bravo.Application.Electoral.Contracts;
using Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Electoral;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Extensions;

public static class ElectoralRepositoryExtensions
{
    public static IServiceCollection AddElectoralRepositories(this IServiceCollection services)
    {
        return services.AddScoped<IElectionRepository, ElectionRepository>()
                       .AddScoped<IElectoralReferenceRepository, ElectoralReferenceRepository>()
                       .AddScoped<ICoalitionRepository, CoalitionRepository>()
                       .AddScoped<ICandidateRepository, CandidateRepository>()
                       .AddScoped<IElectoralStructureRepository, ElectoralStructureRepository>()
                       .AddScoped<IElectionResultRepository, ElectionResultRepository>()
                       .AddScoped<ITseMunicipalityCodeRepository, TseMunicipalityCodeRepository>()
                       .AddScoped<IGeographyLookup, GeographyLookup>();
    }
}
