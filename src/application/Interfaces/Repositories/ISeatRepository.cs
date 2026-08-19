using EventReservation.Domain.Models;

namespace EventReservation.Application.Interfaces.Repositories;

public interface ISeatRepository
{
    Task<Result<Seat>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<Seat>>> GetByVenueIdAsync(Guid venueId, CancellationToken cancellationtoken = default);
    Task<Result<Seat>> AddAsync(Seat seat, CancellationToken cancellationToken = default);
    Task<Result<bool>> TryHoldAsync(Guid seatId, CancellationToken cancellationToken = default);
    Task<Result<bool>> TryReserveAsync(Guid seatId, CancellationToken cancellationToken = default);
    Task<Result<bool>> TryReleaseAsync(Guid seatId, CancellationToken cancellationToken = default);
}