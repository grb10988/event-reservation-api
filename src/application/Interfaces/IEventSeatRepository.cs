using EventReservation.Domain.Models;

namespace EventReservation.Application.Interfaces;

public interface IEventSeatRepository : IRepository
{
    Task<Result<EventSeat>> GetByIdAsync(Guid seatId, Guid eventId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<EventSeat>>> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<Result> ProvisionAsync(Guid eventId, Guid venueId, CancellationToken cancellationToken = default);
    Task<Result<bool>> TryHoldAsync(Guid seatId, Guid eventId, Guid reservationId, CancellationToken cancellationToken = default);
    Task<Result<bool>> TryReserveAsync(Guid seatId, Guid eventId, Guid reservationId, CancellationToken cancellationToken = default);
    Task<Result<bool>> TryReleaseAsync(Guid seatId, Guid eventId, Guid reservationId, CancellationToken cancellationToken = default);
}