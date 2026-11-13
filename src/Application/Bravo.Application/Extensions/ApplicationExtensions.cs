using Bravo.Application.Behaviors;
using FluentValidation;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.Application.Extensions;

public static class ApplicationExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services.AddMediatorPattern()
                       .AddPipelineBehaviors()
                       .AddValidators();
    }
    /// <summary>
    /// Mediator gets the current assembly (the one he is installed/called)
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddMediatorPattern(this IServiceCollection services)
    {
        return services.AddMediator(cfg => cfg.ServiceLifetime = ServiceLifetime.Scoped);
    }

    public static IServiceCollection AddValidators(this IServiceCollection services)
    {
        return services.AddValidatorsFromAssembly(typeof(ApplicationExtensions).Assembly, includeInternalTypes: true);
    }
    public static IServiceCollection AddPipelineBehaviors(this IServiceCollection services)
    {
        return services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    }
}
