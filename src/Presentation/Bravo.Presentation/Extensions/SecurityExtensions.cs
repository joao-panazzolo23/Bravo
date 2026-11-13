using Bravo.Domain.Auth.Enums;
using Bravo.Infrastructure.Auth.Extensions;
using Microsoft.AspNetCore.Authorization;
using System.Threading.RateLimiting;

namespace Bravo.Presentation.Extensions;

/// <summary>
/// Authentication itself (JWT Bearer) is registered by <see cref="AuthExtensions.AddSecurityServices"/>,
/// because it depends on internal options of the Auth project.
/// </summary>
public static class SecurityExtensions
{
    public const string CorsPolicy = "AllowedOrigins";
    public const string AuthenticationRateLimit = "authentication";

    public static IServiceCollection AddSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddSecurityServices()
                       .AddAuthorization()
                       .AddCors(configuration)
                       .AddRateLimiting();
    }

    /// <summary>
    /// Secure by default: every endpoint requires an authenticated user.
    /// Public endpoints must declare [AllowAnonymous].
    /// </summary>
    public static IServiceCollection AddAuthorization(this IServiceCollection services)
    {
        var builder = services.AddAuthorizationBuilder()
                              .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                                  .RequireAuthenticatedUser()
                                  .Build());

        foreach (var role in Enum.GetNames<RoleType>())
            builder.AddPolicy(role, policy => policy.RequireRole(role));

        return services;
    }

    /// <summary>
    /// Cors is a mechanism of security as well
    /// 
    /// For more CORS specs in .NET, see:
    /// https://learn.microsoft.com/pt-br/aspnet/core/security/cors?view=aspnetcore-10.0
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configuration"></param>
    /// <returns></returns>
    public static IServiceCollection AddCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];

        return services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicy, builder =>
            {
                builder.WithOrigins(origins)
                       .AllowAnyMethod()
                       .AllowAnyHeader();
            });
        });
    }

    /// <summary>
    /// Behind a reverse proxy / load balancer, RemoteIpAddress is the proxy address.
    /// Configure UseForwardedHeaders (KnownProxies/KnownNetworks) before the rate limiter,
    /// otherwise every user shares the same limit.
    /// </summary>
    public static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(AuthenticationRateLimit, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1)
                    }));
        });
    }
}
