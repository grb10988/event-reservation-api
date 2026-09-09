namespace EventReservation.Application.Abstractions.IntegrationEvents;

public interface IIntegrationEventDispatcher
{
    Task<Result> DispatchAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent;
}