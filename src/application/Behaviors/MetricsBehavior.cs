using System.Diagnostics;
using System.Diagnostics.Metrics;
using EventReservation.Application.Abstractions;

namespace EventReservation.Application.Behaviors;

public sealed class MetricsBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly Meter Meter = new("EventReservation.Application");
    private static readonly Counter<long> RequestCounter = Meter.CreateCounter<long>("requests.total");
    private static readonly Counter<long> ExceptionCounter = Meter.CreateCounter<long>("requests.exceptions");
    private static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>("requests.duration.ms");

    public async Task<Result<TResponse>> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await next();
            RequestCounter.Add(1,
                new KeyValuePair<string, object?>("request", requestName),
                new KeyValuePair<string, object?>("outcome", result.IsSuccess ? "success" : "failure"));

            return result;
        }
        catch (Exception ex)
        {
            ExceptionCounter.Add(1,
                new KeyValuePair<string, object?>("request", requestName),
                new KeyValuePair<string, object?>("exception_type", ex.GetType().Name));

            throw;
        }
        finally
        {
            RequestDuration.Record(
                stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("request", requestName));

        }
    }
}