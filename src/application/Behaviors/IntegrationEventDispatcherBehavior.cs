using System.Diagnostics;
using System.Diagnostics.Metrics;
using EventReservation.Application.Abstractions.IntegrationEvents;
using Microsoft.Extensions.Logging;

namespace EventReservation.Application.Behaviors;

public sealed class IntegrationEventDispatcherLogging(
    IIntegrationEventDispatcher inner,
    ILogger<IntegrationEventDispatcherLogging> logger) : IIntegrationEventDispatcher
{
    public async Task<Result> DispatchAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        var name = typeof(TEvent).Name;
        var stopwatch = Stopwatch.StartNew();

        PipelineLogging.LogStart(logger, name);

        try
        {
            var result = await inner.DispatchAsync(@event, cancellationToken);
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

public sealed class IntegrationEventDispatcherTracing(IIntegrationEventDispatcher inner)
    : IIntegrationEventDispatcher
{
    public static readonly ActivitySource ActivitySource = new("EventReservation.Application.IntegrationEvents.Consumers");

    public async Task<Result> DispatchAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        using var activity = ActivitySource.StartActivity($"dispatch {typeof(TEvent).Name}");

        try
        {
            var result = await inner.DispatchAsync(@event, cancellationToken);
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

public sealed class IntegrationEventDispatcherMetrics(IIntegrationEventDispatcher inner)
    : IIntegrationEventDispatcher
{
    private static readonly (
        Meter Meter,
        Counter<long> Total,
        Counter<long> Exceptions,
        Histogram<double> duration) Instruments =
        PipelineMetrics.CreateInstruments(
            "EventReservation.Appplication.IntegrationEvents.Consumers",
            "integration_event_consumers");

    public async Task<Result> DispatchAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        var name = typeof(TEvent).Name;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await inner.DispatchAsync(@event, cancellationToken);

            Instruments.Total.Add(1,
                new KeyValuePair<string, object?>("event", name),
                new KeyValuePair<string, object?>("outcome", result.IsSuccess ? "success" : "failure"));

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
            Instruments.duration.Record(
                stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("event", name));
        }
    }
}