namespace EventReservation.Application.Abstractions.Requests;

internal abstract class RequestHandlerBase<TResponse>
{
    public abstract Task<Result<TResponse>> HandleAsync(IRequest<TResponse> request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}