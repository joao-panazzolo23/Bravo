using Bravo.Application.Behaviors;
using Bravo.Application.Importing.Services;
using Bravo.Shared.Notifications;
using FluentValidation;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

using Bravo.Application.Notifications;
namespace Bravo.Application.Extensions;

public static class ApplicationExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services.AddMediatorPattern()
                       .AddPipelineBehaviors()
                       .AddValidators()
                      .AddApplicationServices();
    }

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
        return services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>))
                    .AddScoped<INotificationContext, NotificationContext>();
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        return services.AddScoped<ImportJobStarter>()
                       .AddScoped<ImportRetryPolicy>();
    }
}
