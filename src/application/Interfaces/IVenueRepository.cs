using EventReservation.Domain.Models;

namespace EventReservation.Application.Interfaces;

public interface IVenueRepository
{
    Task<Result<Venue>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<Venue>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<Venue>> AddAsync(Venue venue, CancellationToken cancellationToken = default);
    Task<Result<Venue>> UpdateAsync(Venue venue, CancellationToken cancellationToken = default);
}