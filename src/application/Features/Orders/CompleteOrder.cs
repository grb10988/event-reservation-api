using EventReservation.Application.Abstractions;
using EventReservation.Application.Abstractions.Requests;
using EventReservation.Application.Features.Reservations;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Application.Features.Orders;

public sealed record CompleteOrderResult(Guid Id, string ConfirmationNumber);
public sealed record CompleteOrderCommand(Guid Id) : ICommand<CompleteOrderResult>;

public sealed class CompleteOrderCommandHandler(
    IOrderRepository orderRepository,
    IReservationRepository reservationRepository,
    IDispatcher dispatcher)
    : ICommandHandler<CompleteOrderCommand, CompleteOrderResult>
{
    public Task<Result<CompleteOrderResult>> HandleAsync(
        CompleteOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = orderRepository.GetByIdAsync(command.Id, cancellationToken)
            .Bind(order => order.Complete())
            .Bind(order => CompleteAndPersistAsync(order, cancellationToken));

        return result;
    }

    private Task<Result<CompleteOrderResult>> CompleteAndPersistAsync(Order order, CancellationToken cancellationToken)
    {
        var result = ConfirmRemainingReservationsAsync(order, cancellationToken)
            .Bind(() => order.ConfirmationNumber is string confirmationNumber
                ? Success(confirmationNumber)
                : Failure<string>(Errors.OrderUpdateFailed))
            .Bind(confirmationNumber => orderRepository.TryCompleteAsync(order.Id, confirmationNumber, cancellationToken)
                .Ensure(completed => completed, Errors.OrderNoLongerCompletable)
                .Map(_ => new CompleteOrderResult(order.Id, confirmationNumber)));

        return result;
    }

    private Task<Result> ConfirmRemainingReservationsAsync(Order order, CancellationToken cancellationToken)
    {
        var result = Combine(order.ReservationIds
                .Select(id => reservationRepository.GetByIdAsync(id, cancellationToken))
                .ToArray())
            .Ensure(reservations =>
                reservations.All(r =>
                    r.Status is ReservationStatus.Held or ReservationStatus.Confirmed), Errors.ReservationNoLongerActive)
            .Map(reservations => reservations.Where(r => r.Status == ReservationStatus.Held).ToList())
            .Bind(toConfirm => DispatchConfirmationsAsync(toConfirm, cancellationToken));

        return result;
    }

    private async Task<Result> DispatchConfirmationsAsync(
        IReadOnlyList<Reservation> toConfirm,
        CancellationToken cancellationToken)
    {
        var confirmations = await Task.WhenAll(toConfirm.Select(r =>
            dispatcher.SendAsync(new ConfirmReservationCommand(r.Id), cancellationToken)
                .Bind(_ => Success())));

        var result = Combine(confirmations);

        return result;
    }

    public static class Errors
    {
        private const string Context = "COMPLETE_ORDER";

        public static ResultError ReservationNoLongerActive =>
            new(Context, "One or more reservations on this order are no longer held or confirmed.", ErrorCategory.Conflict);

        public static ResultError OrderUpdateFailed =>
            new(Context, "The order's reservations were confirmed, but the confirmation number could not be generated.", ErrorCategory.Unexpected);

        public static ResultError OrderNoLongerCompletable =>
            new(Context, "The order is no longer pending, or one of its reservations is no longer confirmed.", ErrorCategory.Conflict);
    }
}