using Microsoft.Extensions.DependencyInjection;

namespace EventReservation.Application.Abstractions.IntegrationEvents;

public sealed class IntegrationEventDispatcher(IServiceScopeFactory scopeFactory) : IIntegrationEventDispatcher
{
    public async Task<Result> DispatchAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        using var scope = scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IIntegrationEventHandler<TEvent>>();
        return await handler.HandleAsync(@event, cancellationToken);
    }
}