namespace EventReservation.Application.Abstractions.IntegrationEvents;

public interface IIntegrationEventHandler<in TEvent>
    where TEvent : class, IIntegrationEvent
{
    Task<Result> HandleAsync(TEvent @event, CancellationToken cancellationToken);
}