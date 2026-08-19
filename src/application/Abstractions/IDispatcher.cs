namespace EventReservation.Application.Abstractions;

public interface IDispatcher
{
    Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}