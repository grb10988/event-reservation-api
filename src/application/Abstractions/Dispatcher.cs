using System.Collections.Concurrent;
using System.Linq.Expressions;
using EventReservation.Application.Abstractions.DomainEvents;
using EventReservation.Application.Abstractions.Requests;

namespace EventReservation.Application.Abstractions;

public sealed class Dispatcher(IServiceProvider serviceProvider) : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, object> RequestWrapperCache = new();
    private static readonly ConcurrentDictionary<Type, object> EventWrapperCache = new();

    public Task<Result<TResponse>> SendAsync<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        var wrapper = (RequestHandlerBase<TResponse>)RequestWrapperCache.GetOrAdd(
            request.GetType(),
            CreateRequestWrapper<TResponse>);

        return wrapper.HandleAsync(request, serviceProvider, cancellationToken);
    }

    public Task<Result> PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent
    {
        var wrapper = (DomainEventHandlerBase)EventWrapperCache.GetOrAdd(
            @event.GetType(),
            CreateEventWrapper);

        return wrapper.HandleAsync(@event, serviceProvider, cancellationToken);
    }

    private static object CreateRequestWrapper<TResponse>(Type requestType)
    {
        var wrapperType = typeof(ICommand<TResponse>).IsAssignableFrom(requestType)
            ? typeof(CommandHandlerWrapper<,>).MakeGenericType(requestType, typeof(TResponse))
            : typeof(QueryHandlerWrapper<,>).MakeGenericType(requestType, typeof(TResponse));

        var constructor = wrapperType.GetConstructor(Type.EmptyTypes)
            ?? throw new InvalidOperationException($"{wrapperType.Name} has no parameterless constructor.");

        var lambda = Expression.Lambda<Func<object>>(Expression.New(constructor));
        return lambda.Compile()();
    }

    private static object CreateEventWrapper(Type eventType)
    {
        var wrapperType = typeof(DomainEventHandlerWrapper<>).MakeGenericType(eventType);

        var constructor = wrapperType.GetConstructor(Type.EmptyTypes)
            ?? throw new InvalidOperationException($"{wrapperType.Name} has no parameterless constructor.");

        var lambda = Expression.Lambda<Func<object>>(Expression.New(constructor));
        return lambda.Compile()();
    }
}