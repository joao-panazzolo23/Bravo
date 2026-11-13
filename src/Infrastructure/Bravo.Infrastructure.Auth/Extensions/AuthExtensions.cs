using Bravo.Domain.Auth.Services;
using Bravo.Infrastructure.Auth.Options;
using Bravo.Infrastructure.Auth.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Bravo.Infrastructure.Auth.Extensions;

public static class AuthExtensions
{
    public static IServiceCollection AddSecurityServices(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        return services.AddJwtOptions()
                       .AddRecoveryOptions()
                       .AddPasswordQuality()
                       .AddJwtAuthentication()
                       .AddScoped<ITokenService, TokenService>()
                       .AddScoped<IRecoveryService, RecoveryService>()
                       .AddScoped<IPasswordService, PasswordService>();
    }

    private static IServiceCollection AddJwtOptions(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>()
                .BindConfiguration("Authentication:JWT")
                .ValidateDataAnnotations()
                .ValidateOnStart();

        return services;
    }

    private static IServiceCollection AddRecoveryOptions(this IServiceCollection services)
    {
        services.AddOptions<RecoveryOptions>()
                .BindConfiguration("Authentication:Recovery")
                .ValidateDataAnnotations()
                .ValidateOnStart();

        return services;
    }

    private static IServiceCollection AddPasswordQuality(this IServiceCollection services)
    {
        services.AddOptions<PasswordPolicyOptions>()
                .BindConfiguration("Authentication:PasswordPolicy")
                .ValidateDataAnnotations()
                .ValidateOnStart();

        return services.AddSingleton<IPasswordQualityValidator, PasswordQualityValidator>();
    }

    private static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IOptions<JwtOptions>, IHostEnvironment>((bearer, jwt, environment) =>
                {
                    bearer.RequireHttpsMetadata = !environment.IsDevelopment();
                    bearer.MapInboundClaims = false;
                    bearer.TokenValidationParameters = jwt.Value.AccessValidationParameters();
                });

        return services;
    }
}
