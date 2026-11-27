using Bravo.Application.Analytics.Queries;
using Bravo.Application.Behaviors;
using Bravo.Application.Notifications;
using Bravo.Shared.Notifications;
using FluentValidation;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.Application.Analytics;

/// <summary>
/// Registers only the analytics read model.
/// </summary>
/// <remarks>
/// The MCP host and the web API both expose analytics, but neither needs the import worker,
/// acquisition clients or email sender to do so. This composer keeps that boundary explicit: a
/// host that only answers analytical questions depends on the Domain contract and this module,
/// not on the ingestion pipeline.
/// </remarks>
public static class AnalyticsExtensions
{
    public static IServiceCollection AddElectionAnalytics(this IServiceCollection services)
    {
        return services.AddMediator(cfg => cfg.ServiceLifetime = ServiceLifetime.Scoped)
                       .AddValidatorsFromAssemblyContaining<FindVoteConcentrationQuery>(
                           includeInternalTypes: true)
                       .AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>))
                       .AddScoped<INotificationContext, NotificationContext>();
    }
}
