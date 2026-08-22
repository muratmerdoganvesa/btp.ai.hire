using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace HireLens.Api.Hosting;

public static class Observability
{
    public static IHostBuilder UseHireLensSerilog(this IHostBuilder host) =>
        host.UseSerilog((context, logger) =>
        {
            logger.ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("service", "hirelens-api")
                .WriteTo.Console();
        });

    public static IServiceCollection AddHireLensTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var otlp = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("hirelens-api"))
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation();
                tracing.AddHttpClientInstrumentation();
                if (!string.IsNullOrWhiteSpace(otlp))
                {
                    tracing.AddOtlpExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation();
                metrics.AddHttpClientInstrumentation();
                if (!string.IsNullOrWhiteSpace(otlp))
                {
                    metrics.AddOtlpExporter();
                }
            });

        return services;
    }
}
