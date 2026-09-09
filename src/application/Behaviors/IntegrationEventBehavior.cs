using System.Diagnostics;
using System.Diagnostics.Metrics;
using EventReservation.Application.Abstractions.IntegrationEvents;
using Microsoft.Extensions.Logging;

namespace EventReservation.Application.Behaviors;

public sealed class IntegrationEventLoggingBehavior(
    IEventPublisher inner,
    ILogger<IntegrationEventLoggingBehavior> logger) : IEventPublisher
{
    public async Task<Result> PublishAsync<TEvent>(
        TEvent @event,
        CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        var name = typeof(TEvent).Name;
        var stopwatch = Stopwatch.StartNew();

        PipelineLogging.LogStart(logger, name);

        try
        {
            var result = await inner.PublishAsync(@event, cancellationToken);
            stopwatch.Stop();
            PipelineLogging.LogCompleted(logger, name, result, stopwatch.Elapsed);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            PipelineLogging.LogException(logger, name, ex, stopwatch.Elapsed);
            throw;
        }
    }
}

public sealed class IntegrationEventTracingBehavior(IEventPublisher inner) : IEventPublisher
{
    private static readonly ActivitySource ActivitySource = new("EventReservation.Application.IntegrationEvents");

    public async Task<Result> PublishAsync<TEvent>(
        TEvent @event,
        CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        using var activity = ActivitySource.StartActivity(typeof(TEvent).Name);

        try
        {
            var result = await inner.PublishAsync(@event, cancellationToken);
            PipelineTracing.RecordOutcome(activity, result);
            return result;
        }
        catch (Exception ex)
        {
            PipelineTracing.RecordException(activity, ex);
            throw;
        }
    }
}

public sealed class IntegrationEventMetricsBehavior(IEventPublisher inner) : IEventPublisher
{
    private static readonly (
        Meter Meter,
        Counter<long> Total,
        Counter<long> Exceptions,
        Histogram<double> Duration) Instruments =
        PipelineMetrics.CreateInstruments(
            "EventReservation.Application.IntegrationEvents",
            "integration_events");

    public async Task<Result> PublishAsync<TEvent>(
        TEvent @event,
        CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        var name = typeof(TEvent).Name;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await inner.PublishAsync(@event, cancellationToken);

            Instruments.Total.Add(1,
                new KeyValuePair<string, object?>("event", name),
                new KeyValuePair<string, object?>("outcome", result.IsSuccess ? "success" : "failue"));

            return result;
        }
        catch (Exception ex)
        {
            Instruments.Exceptions.Add(1,
                new KeyValuePair<string, object?>("event", name),
                new KeyValuePair<string, object?>("exception_type", ex.GetType().Name));

            throw;
        }
        finally
        {
            Instruments.Duration.Record(
                stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("event", name));
        }
    }
}