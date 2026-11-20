using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Bravo.Presentation.Telemetry;

internal static class OpenTelemetryExtensions
{
    internal static WebApplicationBuilder AddOpenTelemetryConfiguration(this WebApplicationBuilder builder)
    {

        builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource =>
                    resource.AddService("Bravo.Api"))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                // .AddProcessInstrumentation()
                .AddOtlpExporter());


        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.ParseStateValues = true;
            logging.AddOtlpExporter();
        });

        return builder;
    }
}
