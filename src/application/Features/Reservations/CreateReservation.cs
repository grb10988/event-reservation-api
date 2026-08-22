using EventReservation.Application.Abstractions;
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
    ISeatRepository seatRepository,
    TimeProvider timeProvider)
    : ICommandHandler<CreateReservationCommand, CreateReservationResult>
{
    public Task<Result<CreateReservationResult>> HandleAsync(CreateReservationCommand command, CancellationToken cancellationToken = default)
    {
        var result = seatRepository.TryHoldAsync(command.SeatId, cancellationToken)
            .Ensure(held => held, Errors.SeatNotAvailable)
            .Bind(_ => Reservation.Create(
                command.SeatId,
                command.EventId,
                command.CustomerId,
                command.Price,
                timeProvider,
                command.HoldDuration))
            .Bind(reservation => reservationRepository.AddAsync(reservation, cancellationToken))
            .TapError(_ => seatRepository.TryReleaseAsync(command.SeatId, cancellationToken))
            .Map(reservation => new CreateReservationResult(
                reservation.Id,
                reservation.SeatId,
                reservation.EventId,
                reservation.CustomerId,
                reservation.Price,
                reservation.Status,
                reservation.CreatedAt,
                reservation.HoldExpiresAt));

        return result;
    }

    public static class Errors
    {
        private const string Context = "CREATE_RESERVATION";
        public static ResultError SeatNotAvailable =>
            new(Context, "The requested seat is not available to hold.");
    }
}