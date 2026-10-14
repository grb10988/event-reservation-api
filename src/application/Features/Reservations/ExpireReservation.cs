using EventReservation.Application.Abstractions.Requests;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Application.Features.Reservations;

public sealed record ExpireReservationResult(Guid Id);
public sealed record ExpireReservationCommand(Guid Id) : ICommand<ExpireReservationResult>;

public sealed class ExpireReservationCommandHandler(
    IReservationRepository reservationRepository,
    IEventSeatRepository eventSeatRepository)
    : ICommandHandler<ExpireReservationCommand, ExpireReservationResult>
{
    public Task<Result<ExpireReservationResult>> HandleAsync(
            ExpireReservationCommand command,
            CancellationToken cancellationToken = default)
    {
        var result = reservationRepository.GetByIdAsync(command.Id, cancellationToken)
            .Bind(reservation => reservation.Status is ReservationStatus.Cancelled or ReservationStatus.Expired
                ? Task.FromResult(Success(new ExpireReservationResult(reservation.Id)))
                : ExpireAndReleaseAsync(reservation, cancellationToken));

        return result;
    }

    private Task<Result<ExpireReservationResult>> ExpireAndReleaseAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        var result = reservation.Expire()
            .Bind(expired => reservationRepository.TryExpireAsync(expired.Id, cancellationToken)
                .Ensure(expiredInDb => expiredInDb, Errors.ReservationNotHeld)
                .Bind(_ => eventSeatRepository.TryReleaseAsync(
                    expired.SeatId,
                    expired.EventId,
                    expired.Id,
                    cancellationToken))
                .Map(_ => new ExpireReservationResult(expired.Id)));

        return result;
    }

    public static class Errors
    {
        private const string Context = "EXPIRE_RESERVATION";

        public static ResultError ReservationNotHeld =>
            new(Context, "The requested reservation is not currently held and cannot be expired.", ErrorCategory.Conflict);
    }
}