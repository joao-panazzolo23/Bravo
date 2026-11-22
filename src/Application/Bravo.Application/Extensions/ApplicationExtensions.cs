using Bravo.Application.Behaviors;
using Bravo.Application.Imports.Ibge;
using Bravo.Application.Imports.Tse;
using Bravo.Shared.Notifications;
using FluentValidation;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

using Bravo.Application.Notifications;
using Bravo.Application.Imports;
namespace Bravo.Application.Extensions;

public static class ApplicationExtensions
{
    /// <summary>
    /// Composition root for the Application layer.
    /// </summary>
    /// <remarks>
    /// Import orchestration is registered per module: <c>AddImportExecution</c> owns the shared
    /// job lifecycle, and each source owns its facade and stage importers. Mediator handlers are
    /// discovered from this assembly, so moving a handler between folders needs no registration
    /// change.
    /// </remarks>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services.AddMediatorPattern()
                       .AddPipelineBehaviors()
                       .AddValidators()
                       .AddImportExecution()
                       .AddIbgeImport()
                       .AddTseImport();
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
}
