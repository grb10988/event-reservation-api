using Microsoft.Extensions.DependencyInjection;

namespace EventReservation.Application.Abstractions;

internal sealed class CommandHandlerWrapper<TCommand, TResponse> : RequestHandlerBase<TResponse>
    where TCommand : ICommand<TResponse>
{
    public override async Task<Result<TResponse>> HandleAsync(IRequest<TResponse> request, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var command = (TCommand)request;
        var handler = serviceProvider.GetRequiredService<ICommandHandler<TCommand, TResponse>>();
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TCommand, TResponse>>().Reverse();

        RequestHandlerDelegate<TResponse> pipeline = () => handler.HandleAsync(command, cancellationToken);

        foreach (var behavior in behaviors)
        {
            var next = pipeline;
            pipeline = () => behavior.HandleAsync(command, next, cancellationToken);
        }

        return await pipeline();
    }
}