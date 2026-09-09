using EventReservation.Application.Abstractions.DomainEvents;
using EventReservation.Application.Abstractions.Requests;

namespace EventReservation.Application.Abstractions;

public interface IDispatcher
{
    Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
    Task<Result> PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent;
}