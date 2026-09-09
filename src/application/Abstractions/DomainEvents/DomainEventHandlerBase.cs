namespace EventReservation.Application.Abstractions.DomainEvents;

internal abstract class DomainEventHandlerBase
{
    public abstract Task<Result> HandleAsync(IDomainEvent @event, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}