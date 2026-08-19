using EventReservation.Domain.Models;

namespace EventReservation.Application.Interfaces.Repositories;

public interface IEventRepository
{
    Task<Result<Event>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<Event>>> GetByVenueIdAsync(Guid venueId, CancellationToken cancellationToken = default);
    Task<Result<Event>> AddAsync(Event @event, CancellationToken cancellationToken = default);
    Task<Result<Event>> UpdateAsync(Event @event, CancellationToken cancellationToken = default);
    Task<Result<bool>> TryPublishAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<Result<bool>> TryCancelAsync(Guid eventId, CancellationToken cancellationToken = default);
}