using EventReservation.Api.Diagnostics;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace EventReservation.Api.DependencyInjection;

public static class ObservabilityServiceCollectionExtensions
{
    private const string DefaultOtlpEndpoint = "http://localhost:4317";

    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("EventReservation.Api"))
            .AddTracing(configuration)
            .AddMetrics(configuration);

        return services;
    }

    public static ILoggingBuilder AddObservabilityLogging(this ILoggingBuilder logging, IConfiguration configuration)
    {
        logging.AddOpenTelemetry(otel =>
        {
            otel.IncludeFormattedMessage = true;
            otel.IncludeScopes = true;
            otel.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("EventReservation.Api"));
            otel.AddProcessor(new SensitiveLogRedactingProcessor());
            otel.AddOtlpExporter(otlp =>
            {
                otlp.Endpoint = new Uri(configuration["Otlp:Endpoint"] ?? DefaultOtlpEndpoint);
            });
        });

        return logging;
    }

    private static OpenTelemetryBuilder AddTracing(this OpenTelemetryBuilder builder, IConfiguration configuration) =>
        builder.WithTracing(tracing => tracing
            .AddSource("EventReservation.Application.Requests")
            .AddSource("EventReservation.Application.DomainEvents")
            .AddSource("EventReservation.Application.IntegrationEvents")
            .AddSource("EventReservation.Application.IntegrationEvents.Consumers")
            .AddAspNetCoreInstrumentation()
            .AddNpgsql()
            .AddProcessor(new SensitiveActivityRedactingProcessor())
            .AddOtlpExporter(otlp =>
            {
                otlp.Endpoint = new Uri(configuration["Otlp:Endpoint"] ?? DefaultOtlpEndpoint);
            }));

    private static OpenTelemetryBuilder AddMetrics(this OpenTelemetryBuilder builder, IConfiguration configuration) =>
        builder.WithMetrics(metrics => metrics
            .AddMeter("EventReservation.Application")
            .AddMeter("EventReservation.Application.DomainEvents")
            .AddMeter("EventReservation.Application.IntegrationEvents")
            .AddMeter("EventReservation.Application.IntegrationEvents.Consumers")
            .AddAspNetCoreInstrumentation()
            .AddNpgsqlInstrumentation()
            .AddOtlpExporter(otlp =>
            {
                otlp.Endpoint = new Uri(configuration["otlp:Endpoint"] ?? DefaultOtlpEndpoint);
            }));
}