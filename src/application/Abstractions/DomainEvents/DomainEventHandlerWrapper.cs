using Microsoft.Extensions.DependencyInjection;

namespace EventReservation.Application.Abstractions.DomainEvents;

internal sealed class DomainEventHandlerWrapper<TEvent> : DomainEventHandlerBase
    where TEvent : IDomainEvent
{
    public override async Task<Result> HandleAsync(
        IDomainEvent @event,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var typedEvent = (TEvent)@event;
        var handlers = serviceProvider.GetServices<IDomainEventHandler<TEvent>>();
        var behaviors = serviceProvider.GetServices<IDomainEventPipelineBehavior<TEvent>>().Reverse().ToList();

        var results = new List<Result>();

        foreach (var handler in handlers)
        {
            EventHandlerDelegate pipeline = () => handler.HandleAsync(typedEvent, cancellationToken);

            foreach (var behavior in behaviors)
            {
                var next = pipeline;
                pipeline = () => behavior.HandleAsync(typedEvent, next, cancellationToken);
            }

            results.Add(await pipeline());
        }

        return Combine(results.ToArray());
    }
}