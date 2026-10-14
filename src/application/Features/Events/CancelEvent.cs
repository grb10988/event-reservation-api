using EventReservation.Application.Abstractions;
using EventReservation.Application.Abstractions.Requests;
using EventReservation.Application.Features.Orders;
using EventReservation.Application.Features.Reservations;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Application.Features.Events;

public sealed record CancelEventResult(Guid Id);
public sealed record CancelEventCommand(Guid Id) : ICommand<CancelEventResult>;

public sealed class CancelEventCommandHandler(
    IEventRepository eventRepository,
    IReservationRepository reservationRepository,
    IOrderRepository orderRepository,
    IDispatcher dispatcher)
    : ICommandHandler<CancelEventCommand, CancelEventResult>
{
    public Task<Result<CancelEventResult>> HandleAsync(
        CancelEventCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = eventRepository.GetByIdAsync(command.Id, cancellationToken)
            .Bind(evt => evt.Cancel())
            .Bind(evt => eventRepository.TryCancelAsync(evt.Id, cancellationToken)
                .Ensure(claimed => claimed, Errors.EventAlreadyCancelled)
                .Bind(_ => UnwindActiveReservationsAsync(evt.Id, cancellationToken)))
            .Bind(() => Success(new CancelEventResult(command.Id)));

        return result;
    }

    private Task<Result> UnwindActiveReservationsAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var result = reservationRepository.GetByEventIdAsync(eventId, cancellationToken)
            .Map(reservations => reservations
                .Where(r => r.Status is ReservationStatus.Held or ReservationStatus.Confirmed).ToList())
            .Bind(active => SettleActiveReservationsAsync(active, cancellationToken));

        return result;
    }

    private async Task<Result> SettleActiveReservationsAsync(
    IReadOnlyList<Reservation> active,
    CancellationToken cancellationToken)
    {
        var orderLookups = await Task.WhenAll(active.Select(r =>
            orderRepository.GetByReservationIdAsync(r.Id, cancellationToken)));

        var pairs = active.Zip(orderLookups, (reservation, orderResult) => (reservation, orderResult));

        var unownedCancellations = pairs
            .Where(p => p.orderResult.IsFailure)
            .Select(p => dispatcher.SendAsync(new CancelReservationCommand(p.reservation.Id), cancellationToken).Bind(_ => Success()));

        var orderSettlements = pairs
            .Select(p => p.orderResult.Value)
            .OfType<Order>()
            .GroupBy(order => order.Id)
            .Select(g => g.First())
            .Select(order => order.Status switch
            {
                OrderStatus.Pending => dispatcher.SendAsync(
                    new CancelOrderCommand(order.Id), cancellationToken).Bind(_ => Success()),
                OrderStatus.Completed => dispatcher.SendAsync(
                    new RefundOrderCommand(order.Id), cancellationToken).Bind(_ => Success()),
                _ => Task.FromResult(Success())
            });

        var settled = await Task.WhenAll(unownedCancellations.Concat(orderSettlements));
        var result = Combine(settled);

        return result;
    }

    public static class Errors
    {
        private const string Context = "CANCEL_EVENT";

        public static ResultError EventAlreadyCancelled =>
            new(Context, "The requested event is already cancelled.", ErrorCategory.Conflict);
    }
}