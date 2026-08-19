using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace EventReservation.Application.Abstractions;

public sealed class Dispatcher(IServiceProvider serviceProvider) : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, object> WrapperCache = new();

    public Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        var wrapper = (RequestHandlerBase<TResponse>)WrapperCache.GetOrAdd(request.GetType(), CreateWrapper<TResponse>);
        return wrapper.HandleAsync(request, serviceProvider, cancellationToken);
    }

    private static object CreateWrapper<TResponse>(Type requestType)
    {
        var wrapperType = typeof(ICommand<TResponse>).IsAssignableFrom(requestType)
            ? typeof(CommandHandlerWrapper<,>).MakeGenericType(requestType, typeof(TResponse))
            : typeof(QueryHandlerWrapper<,>).MakeGenericType(requestType, typeof(TResponse));

        var constructor = wrapperType.GetConstructor(Type.EmptyTypes)
            ?? throw new InvalidOperationException($"{wrapperType.Name} has no parameterless constructor.");

        var lambda = Expression.Lambda<Func<object>>(Expression.New(constructor));
        return lambda.Compile()();
    }
}