using EventReservation.Application.Abstractions;
using EventReservation.Application.Interfaces;

namespace EventReservation.Application.Features.Reservations;

public sealed record ConfirmReservationResult(Guid Id);
public sealed record ConfirmReservationCommand(Guid Id) : ICommand<ConfirmReservationResult>;

public sealed class ConfirmReservationCommandHandler(
    IReservationRepository reservationRepository,
    ISeatRepository seatRepository)
    : ICommandHandler<ConfirmReservationCommand, ConfirmReservationResult>
{
    public Task<Result<ConfirmReservationResult>> HandleAsync(
        ConfirmReservationCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = reservationRepository.GetByIdAsync(command.Id, cancellationToken)
            .Bind(reservation => reservation.Confirm())
            .Bind(reservation => reservationRepository.TryConfirmAsync(reservation.Id, cancellationToken)
                .Ensure(confirmed => confirmed, Errors.ReservationNotHeld)
                .Bind(_ => seatRepository.TryReserveAsync(reservation.SeatId, cancellationToken))
                .Ensure(reserved => reserved, Errors.SeatCouldNotBeReserved)
                .Map(_ => new ConfirmReservationResult(reservation.Id)));

        return result;
    }

    public static class Errors
    {
        private const string Context = "CONFIRM_RESERVATION";
        public static ResultError ReservationNotHeld =>
            new(Context, "The requested reservation is not currently held and cannot be confirmed.");
        public static ResultError SeatCouldNotBeReserved =>
            new(Context, "The reservation was confirmed, but the associated seat could not be transitions to Reserved.");
    }
}