using System.Diagnostics;
using System.Diagnostics.Metrics;
using EventReservation.Application.Abstractions.DomainEvents;
using Microsoft.Extensions.Logging;

namespace EventReservation.Application.Behaviors;

public sealed class DomainEventLoggingBehavior<TEvent>(
    ILogger<DomainEventLoggingBehavior<TEvent>> logger) : IDomainEventPipelineBehavior<TEvent>
    where TEvent : IDomainEvent
{
    public async Task<Result> HandleAsync(
        TEvent @event,
        EventHandlerDelegate next,
        CancellationToken cancellationToken = default)
    {
        var name = typeof(TEvent).Name;
        var stopwatch = Stopwatch.StartNew();

        PipelineLogging.LogStart(logger, name);

        try
        {
            var result = await next();
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

public sealed class DomainEventTracingBehavior<TEvent> : IDomainEventPipelineBehavior<TEvent>
    where TEvent : IDomainEvent
{
    private static readonly ActivitySource ActivitySource = new("EventReservation.Application.DomainEvents");

    public async Task<Result> HandleAsync(
        TEvent @event,
        EventHandlerDelegate next,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity(typeof(TEvent).Name);

        try
        {
            var result = await next();
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

public sealed class DomainEventMetricsBehavior<TEvent> : IDomainEventPipelineBehavior<TEvent>
    where TEvent : IDomainEvent
{
    private static readonly (
        Meter Meter,
        Counter<long> Total,
        Counter<long> Exceptions,
        Histogram<double> Duration) Instruments =
        PipelineMetrics.CreateInstruments(
            "EventReservation.Application.DomainEvents",
            "domain_events");

    public async Task<Result> HandleAsync(
        TEvent @event,
        EventHandlerDelegate next,
        CancellationToken cancellationToken = default)
    {
        var name = typeof(TEvent).Name;
        var stopwatch = Stopwatch.StartNew();
        var outcome = "success";

        try
        {
            var result = await next();
            outcome = result.IsSuccess ? "success" : "failure";

            Instruments.Total.Add(1,
                new KeyValuePair<string, object?>("event", name),
                new KeyValuePair<string, object?>("outcome", outcome));

            return result;
        }
        catch (Exception ex)
        {
            outcome = "exception";

            Instruments.Exceptions.Add(1,
                new KeyValuePair<string, object?>("event", name),
                new KeyValuePair<string, object?>("exception_type", ex.GetType().Name));

            throw;
        }
        finally
        {
            Instruments.Duration.Record(
                stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("event", name),
                new KeyValuePair<string, object?>("outcome", outcome));
        }
    }
}