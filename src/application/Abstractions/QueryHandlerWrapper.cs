using Microsoft.Extensions.DependencyInjection;

namespace EventReservation.Application.Abstractions;

internal sealed class QueryHandlerWrapper<TQuery, TResponse> : RequestHandlerBase<TResponse>
    where TQuery : IQuery<TResponse>
{
    public override async Task<Result<TResponse>> HandleAsync(IRequest<TResponse> request, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var query = (TQuery)request;
        var handler = serviceProvider.GetRequiredService<IQueryHandler<TQuery, TResponse>>();
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TQuery, TResponse>>().Reverse();

        RequestHandlerDelegate<TResponse> pipeline = () => handler.HandleAsync(query, cancellationToken);

        foreach (var behavior in behaviors)
        {
            var next = pipeline;
            pipeline = () => behavior.HandleAsync(query, next, cancellationToken);
        }

        return await pipeline();
    }
}