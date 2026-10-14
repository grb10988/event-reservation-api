using EventReservation.Application.Abstractions;
using EventReservation.Application.Abstractions.Requests;
using EventReservation.Application.Features.Reservations;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Application.Features.Orders;

public sealed record CancelOrderResult(Guid Id);
public sealed record CancelOrderCommand(Guid Id) : ICommand<CancelOrderResult>;

public sealed class CancelOrderCommandHandler(
    IOrderRepository orderRepository,
    IReservationRepository reservationRepository,
    IDispatcher dispatcher)
    : ICommandHandler<CancelOrderCommand, CancelOrderResult>
{
    public Task<Result<CancelOrderResult>> HandleAsync(
        CancelOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = orderRepository.GetByIdAsync(command.Id, cancellationToken)
            .Bind(order => order.Cancel())
            .Bind(order => orderRepository.TryCancelAsync(order.Id, cancellationToken)
                .Ensure(claimed => claimed, Errors.OrderNoLongerPending)
                .Bind(_ => CancelActiveReservationsAsync(order, cancellationToken)))
            .Map(order => new CancelOrderResult(order.Id));

        return result;
    }

    private Task<Result<Order>> CancelActiveReservationsAsync(Order order, CancellationToken cancellationToken)
    {
        var result = Combine(order.ReservationIds
                .Select(id => reservationRepository.GetByIdAsync(id, cancellationToken))
                .ToArray())
            .Map(reservations => reservations.Where(r => r.Status is ReservationStatus.Held or ReservationStatus.Confirmed).ToList())
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
        private const string Context = "CANCEL_ORDER";

        public static ResultError OrderNoLongerPending =>
            new(Context, "The order is no longer pending and cannot be cancelled.", ErrorCategory.Conflict);
    }
}