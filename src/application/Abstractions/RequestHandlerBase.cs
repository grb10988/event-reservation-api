namespace EventReservation.Application.Abstractions;

internal abstract class RequestHandlerBase<TResponse>
{
    public abstract Task<Result<TResponse>> HandleAsync(IRequest<TResponse> request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}