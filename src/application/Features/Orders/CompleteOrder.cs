using EventReservation.Application.Abstractions;
using EventReservation.Application.Features.Reservations;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Application.Features.Orders;

public sealed record CompleteOrderResult(Guid Id, string ConfirmationNumber);
public sealed record CompleteOrderCommand(Guid Id) : ICommand<CompleteOrderResult>;

public sealed class CompleteOrderCommandHandler(
    IOrderRepository orderRepository,
    IDispatcher dispatcher)
    : ICommandHandler<CompleteOrderCommand, CompleteOrderResult>
{
    public async Task<Result<CompleteOrderResult>> HandleAsync(
        CompleteOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await orderRepository.GetByIdAsync(command.Id, cancellationToken)
            .Bind(order => order.Complete())
            .Bind(order => ConfirmReservationsAsync(order, cancellationToken))
            .Bind(order => order.ConfirmationNumber is string confirmationNumber
                ? Success((Order: order, ConfirmationNumber: confirmationNumber))
                : Failure<(Order Order, string ConfirmationNumber)>(Errors.OrderUpdateFailed))
            .Bind(t => orderRepository.TryCompleteAsync(t.Order.Id, t.ConfirmationNumber, cancellationToken)
                .Ensure(completed => completed, Errors.OrderUpdateFailed)
                .Map(_ => new CompleteOrderResult(t.Order.Id, t.ConfirmationNumber)));

        return result;
    }

    private async Task<Result<Order>> ConfirmReservationsAsync(Order order, CancellationToken cancellationToken)
    {
        var confirmations = await Task.WhenAll(order.ReservationIds.Select(id =>
            dispatcher.SendAsync(new ConfirmReservationCommand(id), cancellationToken)));

        var confirmed = Combine(confirmations);

        return confirmed.IsSuccess
            ? Success(order)
            : Failure<Order>(confirmed.Errors);
    }

    public static class Errors
    {
        private const string Context = "COMPLETE_ORDER";
        public static ResultError OrderUpdateFailed =>
            new(Context, "The order's reservations were confirmed, but the order itself could not be marked Completed.");
    }
}