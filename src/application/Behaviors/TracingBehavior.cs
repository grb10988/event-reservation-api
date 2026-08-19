using System.Diagnostics;
using EventReservation.Application.Abstractions;

namespace EventReservation.Application.Behaviors;

public sealed class TracingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly ActivitySource ActivitySource = new("EventReservation.Application");

    public async Task<Result<TResponse>> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity(typeof(TRequest).Name);

        try
        {
            var result = await next();

            if (result.IsSuccess)
                activity?.SetStatus(ActivityStatusCode.Ok);
            else
            {
                activity?.SetStatus(ActivityStatusCode.Error, string.Join("; ", result.ToFailureMessages()));
                activity?.SetTag("error.type", "business_failure");
            }

            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
            {
                { "exception.type", ex.GetType().FullName },
                { "exception.message", ex.Message },

                { "exception.stacktrace", ex.StackTrace }
            }));
            throw;
        }
    }
}