using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Diagnostics.CodeAnalysis;

namespace SlimBus.Api.Configs;

[ExcludeFromCodeCoverage]
internal static class LogConfigs
{
    public static WebApplicationBuilder AddLogConfig(this WebApplicationBuilder builder, FeatureOptions features)
    {
        if (!features.EnableOpenTelemetry)
        {
#if DEBUG
            builder.Logging.AddConsole();
#endif
            return builder;
        }

        builder.Logging.ClearProviders();
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        var otelBuilder = builder.Services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
#if DEBUG
                tracing.AddConsoleExporter();
#endif
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
#if DEBUG
                metrics.AddConsoleExporter();
#endif
            });

        if (!string.IsNullOrWhiteSpace(builder.Configuration.GetValue<string>("OTEL_EXPORTER_OTLP_ENDPOINT")))
            otelBuilder.UseOtlpExporter();


        if (!string.IsNullOrWhiteSpace(builder.Configuration.GetValue<string>("AzureMonitor:ConnectionString")))
            otelBuilder.UseAzureMonitor();

        return builder;
    }
}