using EventReservation.Application.Abstractions;
using EventReservation.Application.Features.Reservations;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Application.Features.Orders;

public sealed record CancelOrderResult(Guid Id);
public sealed record CancelOrderCommand(Guid Id) : ICommand<CancelOrderResult>;

public sealed class CancelOrderCommandHandler(
    IOrderRepository orderRepository,
    IDispatcher dispatcher)
    : ICommandHandler<CancelOrderCommand, CancelOrderResult>
{
    public async Task<Result<CancelOrderResult>> HandleAsync(
        CancelOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await orderRepository.GetByIdAsync(command.Id, cancellationToken)
            .Bind(order => order.Cancel())
            .Bind(order => CancelReservationsAsync(order, cancellationToken))
            .Bind(order => orderRepository.TryCancelAsync(order.Id, cancellationToken)
                .Ensure(cancelled => cancelled, Errors.OrderUpdateFailed)
                .Map(_ => new CancelOrderResult(order.Id)));

        return result;
    }

    private async Task<Result<Order>> CancelReservationsAsync(Order order, CancellationToken cancellationToken)
    {
        var cancellations = await Task.WhenAll(order.ReservationIds.Select(id =>
            dispatcher.SendAsync(new CancelReservationCommand(id), cancellationToken)));

        var cancelled = Combine(cancellations);

        return cancelled.IsSuccess
            ? Success(order)
            : Failure<Order>(cancelled.Errors);
    }

    public static class Errors
    {
        private const string Context = "CANCEL_ORDER";
        public static ResultError OrderUpdateFailed =>
            new(Context, "The order's reservations were cancelled, but the order itself could not be marked Cancelled.");
    }
}