using EventReservation.Domain.Models;

namespace EventReservation.Application.Interfaces;

public interface ISeatRepository : IRepository
{
    Task<Result<Seat>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<Seat>>> GetByVenueIdAsync(Guid venueId, CancellationToken cancellationToken = default);
    Task<Result<Seat>> AddAsync(Seat seat, CancellationToken cancellationToken = default);
}