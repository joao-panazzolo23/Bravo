using Bravo.Application.Exceptions;
using Bravo.Presentation.Exceptions;
using Bravo.Presentation.Exceptions.Strategies.Abstract;
using Microsoft.AspNetCore.Mvc;

namespace Bravo.Presentation.Extensions;

public static class ExceptionHandlerExtensions
{
    public static IServiceCollection AddExceptions(this IServiceCollection services)
    {
        return services.AddExceptionHandler().AddExceptionFactory().AddExceptionStrategies();
    }
    public static IServiceCollection AddExceptionStrategies(this IServiceCollection services)
    {
        var types =
           typeof(ExceptionHandlerExtensions).Assembly
           .GetTypes().Where(x => typeof(IExceptionStrategy).IsAssignableFrom(x) && !x.IsAbstract && x.IsClass);

        foreach (var type in types)
        {
            services.AddTransient(typeof(IExceptionStrategy), type);
        }
        return services;

    }
    public static IServiceCollection AddExceptionHandler(this IServiceCollection services)
    {
        return services.AddExceptionHandler<GlobalExceptionHandler>().AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions.TryAdd("requestId", context.HttpContext.TraceIdentifier);
            };
        });
    }

    /// <summary>
    /// If we wanted to customize errors, we could use this function with a property/errors dictionary. 
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    /// <exception cref="ValidationException"></exception>
    public static IServiceCollection AddExceptionFactory(this IServiceCollection services)
    {
        return services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = new List<ApiError>();

                foreach (var entry in context.ModelState)
                {
                    foreach (var error in entry.Value!.Errors)
                    {
                        var apiError = new ApiError(entry.Key, error.ErrorMessage);
                        errors.Add(apiError);
                    }
                }
                throw new ValidationException(errors);
            };
        });
    }
}
