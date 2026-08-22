using EventReservation.Application.Abstractions;
using EventReservation.Application.Interfaces;

namespace EventReservation.Application.Features.Reservations;

public sealed record ExpireReservationResult(Guid Id);
public sealed record ExpireReservationCommand(Guid Id) : ICommand<ExpireReservationResult>;

public sealed class ExpireReservationCommandHandler(
    IReservationRepository reservationRepository,
    ISeatRepository seatRepository)
    : ICommandHandler<ExpireReservationCommand, ExpireReservationResult>
{
    public Task<Result<ExpireReservationResult>> HandleAsync(
            ExpireReservationCommand command,
            CancellationToken cancellationToken = default)
    {
        var result = reservationRepository.GetByIdAsync(command.Id, cancellationToken)
            .Bind(reservation => reservation.Expire())
            .Bind(reservation => reservationRepository.TryExpireAsync(reservation.Id, cancellationToken)
                .Ensure(expired => expired, Errors.ReservationNotHeld)
                .Bind(_ => seatRepository.TryReleaseAsync(reservation.SeatId, cancellationToken))
                .Ensure(released => released, Errors.SeatCouldNotBeReleased)
                .Map(_ => new ExpireReservationResult(reservation.Id)));

        return result;
    }

    public static class Errors
    {
        private const string Context = "EXPIRE_RESERVATION";
        public static ResultError ReservationNotHeld =>
            new(Context, "The requested reservation is not currently held and cannot be expired.");
        public static ResultError SeatCouldNotBeReleased =>
            new(Context, "The reservation was expired, but the associated seat could not be released.");
    }
}