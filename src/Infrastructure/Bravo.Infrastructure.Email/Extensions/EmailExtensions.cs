using Bravo.Domain.Services.Email;
using Bravo.Infrastructure.Email.DTO;
using Bravo.Infrastructure.Email.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.Infrastructure.Email.Extensions;
public static class EmailExtensions
{
    public static IServiceCollection AddEmailSender(this IServiceCollection services, IConfiguration configuration)
    {
        var config = configuration.GetSection("Email");

        services.AddOptions<EmailOptions>()
                .Bind(config)
                .ValidateDataAnnotations()
                .ValidateOnStart();

        services.AddOptions<AccountEmailOptions>()
                .Bind(config.GetSection("Account"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        return services.RegisterEmailService();
    }
    private static IServiceCollection RegisterEmailService(this IServiceCollection services)
    {
        services.AddTransient<IEmailService, EmailService>()
                .AddTransient<IAccountEmailService, AccountEmailService>();
        return services;
    }
}
