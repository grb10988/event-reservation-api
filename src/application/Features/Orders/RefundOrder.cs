using EventReservation.Application.Abstractions;
using EventReservation.Application.Abstractions.Requests;
using EventReservation.Application.Features.Reservations;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Application.Features.Orders;

public sealed record RefundOrderResult(Guid Id);
public sealed record RefundOrderCommand(Guid Id) : ICommand<RefundOrderResult>;

public sealed class RefundOrderCommandHandler(
    IOrderRepository orderRepository,
    IReservationRepository reservationRepository,
    IDispatcher dispatcher)
    : ICommandHandler<RefundOrderCommand, RefundOrderResult>
{
    public Task<Result<RefundOrderResult>> HandleAsync(
        RefundOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = orderRepository.GetByIdAsync(command.Id, cancellationToken)
            .Bind(order => order.Refund())
            .Bind(order => orderRepository.TryRefundAsync(order.Id, cancellationToken)
                .Ensure(claimed => claimed, Errors.OrderNoLongerCompleted)
                .Bind(_ => CancelActiveReservationsAsync(order, cancellationToken))
                .Map(_ => new RefundOrderResult(order.Id)));

        return result;
    }

    private Task<Result<Order>> CancelActiveReservationsAsync(Order order, CancellationToken cancellationToken)
    {
        var result = Combine(order.ReservationIds
                .Select(id => reservationRepository.GetByIdAsync(id, cancellationToken))
                .ToArray())
            .Map(reservations => reservations
                .Where(r => r.Status is ReservationStatus.Held or ReservationStatus.Confirmed).ToList())
            .Bind(active => DispatchCancellationsAsync(active, cancellationToken))
            .Bind(() => Success(order));

        return result;
    }

    private async Task<Result> DispatchCancellationsAsync(IReadOnlyList<Reservation> active, CancellationToken cancellationToken)
    {
        var cancellations = await Task.WhenAll(active.Select(r =>
            dispatcher.SendAsync(new CancelReservationCommand(r.Id), cancellationToken).Bind(_ => Success())));

        var result = Combine(cancellations);

        return result;
    }


    public static class Errors
    {
        private const string Context = "REFUND_ORDER";

        public static ResultError OrderNoLongerCompleted =>
            new(Context, "The order is no longer completed and cannot be refunded.", ErrorCategory.Conflict);
    }
}