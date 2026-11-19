using Bravo.Domain.Electoral.Services;
using Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Electoral;
using Microsoft.Extensions.DependencyInjection;
using Bravo.Domain.Electoral.Repositories;
using Bravo.Domain.Geography.Repositories;

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
                       .AddScoped<IGeographyRepository, GeographyLookup>();
    }
}
