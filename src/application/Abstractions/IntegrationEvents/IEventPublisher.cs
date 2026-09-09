namespace EventReservation.Application.Abstractions.IntegrationEvents;

public interface IEventPublisher
{
    Task<Result> PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent;
}