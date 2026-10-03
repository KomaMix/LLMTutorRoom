using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Shared.Observability;

public static class ObservabilityExtensions
{
    public static WebApplicationBuilder AddProjectObservability(
        this WebApplicationBuilder builder,
        string serviceName)
    {
        var serviceVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString();
        Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(
            new TextMapPropagator[]
            {
                new TraceContextPropagator(),
                new BaggagePropagator()
            }));
        var openTelemetry = builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName,
                serviceNamespace: "LLMTutorRoom",
                serviceVersion: serviceVersion,
                serviceInstanceId: Environment.MachineName))
            .WithTracing(tracing => tracing
                .AddSource(ProjectTelemetry.ActivitySourceName)
                .AddSource(ProjectTelemetry.LlmTelemetryName)
                .AddSource("Npgsql")
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health");
                })
                .AddHttpClientInstrumentation(options =>
                {
                    options.RecordException = true;
                }))
            .WithMetrics(metrics => metrics
                .AddMeter(ProjectTelemetry.MeterName)
                .AddMeter(ProjectTelemetry.LlmTelemetryName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .WithLogging(
                configureBuilder: null,
                configureOptions: logging =>
                {
                    logging.IncludeFormattedMessage = true;
                    logging.IncludeScopes = true;
                    logging.ParseStateValues = true;
                });

        if (builder.Environment.IsDevelopment()
            || !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            openTelemetry.UseOtlpExporter();
        }

        builder.Services.AddSingleton<ProjectMetrics>();
        return builder;
    }
}
