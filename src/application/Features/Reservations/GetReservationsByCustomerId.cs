using EventReservation.Application.Abstractions;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Application.Features.Reservations;

public sealed record ReservationSummary(
    Guid Id,
    Guid SeatId,
    Guid EventId,
    decimal Price,
    ReservationStatus Status,
    DateTimeOffset CreatedAt);

public sealed record GetReservationsByCustomerIdQuery(Guid CustomerId) : IQuery<IReadOnlyList<ReservationSummary>>;

public sealed class GetReservationsByCustomerIdQueryHandler(IReservationRepository reservationRepository)
    : IQueryHandler<GetReservationsByCustomerIdQuery, IReadOnlyList<ReservationSummary>>
{
    public Task<Result<IReadOnlyList<ReservationSummary>>> HandleAsync(
        GetReservationsByCustomerIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = reservationRepository.GetByCustomerIdAsync(query.CustomerId, cancellationToken)
            .Map(reservations => (IReadOnlyList<ReservationSummary>)reservations
                .Select(r => new ReservationSummary(
                    r.Id,
                    r.SeatId,
                    r.EventId,
                    r.Price,
                    r.Status,
                    r.CreatedAt))
                .ToList());

        return result;
    }
}