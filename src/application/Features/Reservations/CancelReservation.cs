using EventReservation.Application.Abstractions.Requests;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Application.Features.Reservations;

public sealed record CancelReservationResult(Guid Id);
public sealed record CancelReservationCommand(Guid Id) : ICommand<CancelReservationResult>;

public sealed class CancelReservationCommandHandler(
    IReservationRepository reservationRepository,
    IEventSeatRepository eventSeatRepository)
    : ICommandHandler<CancelReservationCommand, CancelReservationResult>
{
    public Task<Result<CancelReservationResult>> HandleAsync(
        CancelReservationCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = reservationRepository.GetByIdAsync(command.Id, cancellationToken)
            .Bind(reservation => reservation.Status is ReservationStatus.Cancelled or ReservationStatus.Expired
                ? Task.FromResult(Success(new CancelReservationResult(reservation.Id)))
                : CancelAndReleaseAsync(reservation, cancellationToken));

        return result;
    }

    private Task<Result<CancelReservationResult>> CancelAndReleaseAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        var result = reservation.Cancel()
            .Bind(cancelled => reservationRepository.TryCancelAsync(cancelled.Id, cancellationToken)
                .Ensure(cancelledInDb => cancelledInDb, Errors.ReservationNotHeldOrConfirmed)
                .Bind(_ => eventSeatRepository.TryReleaseAsync(
                    cancelled.SeatId,
                    cancelled.EventId,
                    cancelled.Id,
                    cancellationToken))
                .Map(_ => new CancelReservationResult(cancelled.Id)));

        return result;
    }

    public static class Errors
    {
        private const string Context = "CANCEL_RESERVATION";

        public static ResultError ReservationNotHeldOrConfirmed =>
            new(Context, "The requested reservation is not currently held or confirmed and cannot be cancelled.", ErrorCategory.Conflict);
    }
}