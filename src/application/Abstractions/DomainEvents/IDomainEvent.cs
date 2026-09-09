namespace EventReservation.Application.Abstractions.DomainEvents;

public interface IDomainEvent { }

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task<Result> HandleAsync(TEvent @event, CancellationToken cacellationToken = default);
}

public delegate Task<Result> EventHandlerDelegate();

public interface IDomainEventPipelineBehavior<TEvent> where TEvent : IDomainEvent
{
    Task<Result> HandleAsync(TEvent @event, EventHandlerDelegate next, CancellationToken cancellationToken = default);
}