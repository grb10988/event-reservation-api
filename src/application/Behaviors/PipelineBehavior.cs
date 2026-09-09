using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace EventReservation.Application.Behaviors;

public static class PipelineLogging
{
    public static void LogStart(ILogger logger, string name) =>
        logger.LogInformation("Handling {Name}", name);

    public static void LogCompleted(ILogger logger, string name, Result result, TimeSpan elapsed)
    {
        if (result.IsSuccess)
            logger.LogInformation(
                "Handled {Name} successfulling in {ElapsedMilliseconds:F2}ms",
                name,
                elapsed.TotalMilliseconds);
        else
            logger.LogWarning(
                "Handled {Name} with failure in {ElapsedMilliseconds:F2}ms: {Errors}",
                name,
                elapsed.TotalMilliseconds,
                string.Join("; ", result.ToFailureMessages()));
    }

    public static void LogException(ILogger logger, string name, Exception ex, TimeSpan elapsed) =>
        logger.LogError(
            ex,
            "Unhandled exception in {Name} after {ElapsedMilliseconds:F2}ms",
            name,
            elapsed.TotalMilliseconds);
}

public static class PipelineTracing
{
    public static void RecordOutcome(Activity? activity, Result result)
    {
        if (result.IsSuccess)
            activity?.SetStatus(ActivityStatusCode.Ok);
        else
        {
            activity?.SetStatus(ActivityStatusCode.Error, string.Join("; ", result.ToFailureMessages()));
            activity?.SetTag("error.type", "business_failure");
        }
    }

    public static void RecordException(Activity? activity, Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.SetTag("error.type", ex.GetType().Name);
        activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
        {
            { "exception.type", ex.GetType().FullName },
            { "exception.message", ex.Message },
            { "exception.stacktrace", ex.StackTrace }
        }));
    }
}

public static class PipelineMetrics
{
    public static (
        Meter Meter,
        Counter<long> Total,
        Counter<long> Exceptions,
        Histogram<double> duration) CreateInstruments(string meterName, string metricPrefix)
    {
        var meter = new Meter(meterName);
        var total = meter.CreateCounter<long>($"{metricPrefix}.total");
        var exceptions = meter.CreateCounter<long>($"{metricPrefix}.exceptions");
        var duration = meter.CreateHistogram<double>($"{metricPrefix}.duration.ms");

        return (meter, total, exceptions, duration);
    }
}