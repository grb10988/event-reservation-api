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
                "Handled {Name} successfully in {ElapsedMilliseconds:F2}ms",
                name,
                elapsed.TotalMilliseconds);
        else
            logger.LogWarning(
                "Handled {Name} with failure in {ElapsedMilliseconds:F2}ms. Contexts: {ErrorContexts}. Errors: {Errors}",
                name,
                elapsed.TotalMilliseconds,
                result.ToContexts(),
                result.ToDiagnosticMessages());
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
            activity?.SetStatus(ActivityStatusCode.Error, result.ToDiagnosticMessages());
            activity?.SetTag("error.type", "business_failure");
            activity?.SetTag("error.contexts", result.ToContexts());
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

    public static KeyValuePair<string, object?>[] BuildOutcomeTags(
        string dimensionName,
        string name,
        string outcome,
        Result result) =>
        outcome == "failure"
            ? new[]
                {
                    new KeyValuePair<string, object?>(dimensionName, name),
                    new KeyValuePair<string, object?>("outcome", outcome),
                    new KeyValuePair<string, object?>("error_context", result.Errors.Select(e => e.Context).FirstOrDefault())
                }
            : new[]
                {
                    new KeyValuePair<string, object?>(dimensionName, name),
                    new KeyValuePair<string, object?>("outcome", outcome)
                };

    public static KeyValuePair<string, object?>[] BuildExceptionTags(
        string dimensionName,
        string name,
        Exception ex) =>
        new[]
        {
            new KeyValuePair<string, object?>(dimensionName, name),
            new KeyValuePair<string, object?>("exception_type", ex.GetType().Name)
        };

    public static KeyValuePair<string, object?>[] BuildDurationTags(
        string dimensionName,
        string name,
        string outcome) =>
        new[]
        {
            new KeyValuePair<string, object?>(dimensionName, name),
            new KeyValuePair<string, object?>("outcome", outcome)
        };
}