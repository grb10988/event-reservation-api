using EventReservation.Application.Abstractions.Requests;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Application.Features.Reservations;

public sealed record CreateReservationResult(
    Guid Id,
    Guid SeatId,
    Guid EventId,
    Guid CustomerId,
    decimal Price,
    ReservationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset HoldExpiresAt);

public sealed record CreateReservationCommand(
    Guid SeatId,
    Guid EventId,
    Guid CustomerId,
    decimal Price,
    TimeSpan? HoldDuration = null) : ICommand<CreateReservationResult>;

public sealed class CreateReservationCommandHandler(
    IReservationRepository reservationRepository,
    IEventSeatRepository eventSeatRepository,
    TimeProvider timeProvider)
    : ICommandHandler<CreateReservationCommand, CreateReservationResult>
{
    public Task<Result<CreateReservationResult>> HandleAsync(
        CreateReservationCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = Reservation.Create(
                command.SeatId,
                command.EventId,
                command.CustomerId,
                command.Price,
                timeProvider,
                command.HoldDuration)
            .Bind(reservation => AcquireAndPersistAsync(reservation, cancellationToken));

        return result;
    }

    private Task<Result<CreateReservationResult>> AcquireAndPersistAsync(
        Reservation reservation,
        CancellationToken cancellationToken)
    {
        var result = eventSeatRepository.TryHoldAsync(
                reservation.SeatId,
                reservation.EventId,
                reservation.Id,
                cancellationToken)
            .Ensure(held => held, Errors.SeatNotAvailable)
            .Bind(_ => reservationRepository.AddAsync(reservation, cancellationToken)
                .TapError(_ => eventSeatRepository.TryReleaseAsync(
                    reservation.SeatId,
                    reservation.EventId,
                    reservation.Id,
                    cancellationToken)))
            .Map(added => new CreateReservationResult(
                added.Id,
                added.SeatId,
                added.EventId,
                added.CustomerId,
                added.Price,
                added.Status,
                added.CreatedAt,
                added.HoldExpiresAt));

        return result;
    }

    public static class Errors
    {
        private const string Context = "CREATE_RESERVATION";

        public static ResultError SeatNotAvailable =>
            new(Context, "The requested seat is not available to hold for this event.", ErrorCategory.Conflict);
    }
}