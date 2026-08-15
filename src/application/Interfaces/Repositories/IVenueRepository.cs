using EventReservation.Domain.Models;

namespace EventReservation.Application.Interfaces.Repositories;

public interface IVenueRepository
{
    Task<Venue?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Venue>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Venue venue, CancellationToken cancellationToken = default);
    Task UpdateAsync(Venue venue, CancellationToken cancellationToken = default);
}