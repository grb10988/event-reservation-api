using System.Diagnostics;
using EventReservation.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace EventReservation.Application.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("Handling {RequestName}", requestName);

        try
        {
            var result = await next();
            stopwatch.Stop();

            if (result.IsSuccess)
                logger.LogInformation(
                    "Handled {RequestName} successfully in {ElapsedMilliseconds:F2}ms",
                    requestName, stopwatch.Elapsed.TotalMilliseconds);
            else
                logger.LogWarning(
                    "Handled {RequestName} with failure in {ElapsedMilliseconds:F2}ms: {Errors}",
                    requestName, stopwatch.Elapsed.TotalMilliseconds, string.Join("; ", result.ToFailureMessages()));

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Unhandled exception in {RequestName} after {ElapsedMilliseconds:F2}ms",
            requestName, stopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
    }
}