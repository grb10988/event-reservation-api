using EventReservation.Application.Abstractions;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Application.Features.Reservations;

public sealed record GetReservationByIdResult(
    Guid Id,
    Guid SeatId,
    Guid EventId,
    Guid CustomerId,
    decimal Price,
    ReservationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset HoldExpiresAt);

public sealed record GetReservationByIdQuery(Guid Id) : IQuery<GetReservationByIdResult>;

public sealed class GetReservationByIdQueryHandler(IReservationRepository reservationRepository) : IQueryHandler<GetReservationByIdQuery, GetReservationByIdResult>
{
    public Task<Result<GetReservationByIdResult>> HandleAsync(GetReservationByIdQuery query, CancellationToken cancellationToken = default)
    {
        var result = reservationRepository.GetByIdAsync(query.Id, cancellationToken)
            .Map(r => new GetReservationByIdResult(
                r.Id,
                r.SeatId,
                r.EventId,
                r.CustomerId,
                r.Price,
                r.Status,
                r.CreatedAt,
                r.HoldExpiresAt));

        return result;
    }
}