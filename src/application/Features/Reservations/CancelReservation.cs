using EventReservation.Application.Abstractions;
using EventReservation.Application.Interfaces;

namespace EventReservation.Application.Features.Reservations;

public sealed record CancelReservationResult(Guid Id);
public sealed record CancelReservationCommand(Guid Id) : ICommand<CancelReservationResult>;

public sealed class CancelReservationCommandHandler(
    IReservationRepository reservationRepository,
    ISeatRepository seatRepository)
    : ICommandHandler<CancelReservationCommand, CancelReservationResult>
{
    public Task<Result<CancelReservationResult>> HandleAsync(
        CancelReservationCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = reservationRepository.GetByIdAsync(command.Id, cancellationToken)
            .Bind(reservation => reservation.Cancel())
            .Bind(reservation => reservationRepository.TryCancelAsync(reservation.Id, cancellationToken)
                .Ensure(cancelled => cancelled, Errors.ReservationNotHeldOrConfirmed)
                .Bind(_ => seatRepository.TryReleaseAsync(reservation.SeatId, cancellationToken))
                .Ensure(released => released, Errors.SeatCouldNotBeReleased)
                .Map(_ => new CancelReservationResult(reservation.Id)));

        return result;
    }

    public static class Errors
    {
        private const string Context = "CANCEL_RESERVATION";
        public static ResultError ReservationNotHeldOrConfirmed =>
            new(Context, "The requested reservation is not currently held or confirmed and cannot be cancelled.");
        public static ResultError SeatCouldNotBeReleased =>
            new(Context, "The reservation was cancelled, but the associated seat could not be released.");
    }
}