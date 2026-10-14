using EventReservation.Application.Abstractions.Requests;
using EventReservation.Application.Interfaces;

namespace EventReservation.Application.Features.Reservations;

public sealed record ConfirmReservationResult(Guid Id);
public sealed record ConfirmReservationCommand(Guid Id) : ICommand<ConfirmReservationResult>;

public sealed class ConfirmReservationCommandHandler(
    IReservationRepository reservationRepository,
    IEventSeatRepository eventSeatRepository,
    TimeProvider timeProvider)
    : ICommandHandler<ConfirmReservationCommand, ConfirmReservationResult>
{
    public Task<Result<ConfirmReservationResult>> HandleAsync(
        ConfirmReservationCommand command,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        var result = reservationRepository.GetByIdAsync(command.Id, cancellationToken)
            .Bind(reservation => reservation.Confirm())
            .Ensure(reservation => reservation.HoldExpiresAt > now, Errors.HoldExpired)
            .Bind(reservation => eventSeatRepository.TryReserveAsync(
                    reservation.SeatId,
                    reservation.EventId,
                    reservation.Id,
                    cancellationToken)
                .Ensure(reserved => reserved, Errors.SeatNotHeldByReservation)
                .Bind(_ => reservationRepository.TryConfirmAsync(reservation.Id, cancellationToken))
                .Ensure(confirmed => confirmed, Errors.ReservationNotHeld)
                .Map(_ => new ConfirmReservationResult(reservation.Id)));

        return result;
    }

    public static class Errors
    {
        private const string Context = "CONFIRM_RESERVATION";

        public static ResultError ReservationNotHeld =>
            new(Context, "The requested reservation is not currently held and cannot be confirmed.", ErrorCategory.Conflict);

        public static ResultError HoldExpired =>
            new(Context, "The hold on this reservation has expired.", ErrorCategory.Conflict);

        public static ResultError SeatNotHeldByReservation =>
            new(Context, "The seat is no longer held by this reservation.", ErrorCategory.Conflict);
    }
}