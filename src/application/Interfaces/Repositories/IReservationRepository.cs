using EventReservation.Domain.Models;

namespace EventReservation.Application.Interfaces.Repositories;

public interface IReservationRepository
{
    Task<Result<Reservation>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<Reservation>>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<Reservation>>> GetExpiredHoldsAsync(DateTimeOffset asOf, CancellationToken cancellationToken = default);
    Task<Result<Reservation>> AddAsync(Reservation reservation, CancellationToken cancellationToken = default);
    Task<Result<bool>> TryConfirmAsync(Guid reservationId, CancellationToken cancellationToken = default);
    Task<Result<bool>> TryCancelAsync(Guid reservationId, CancellationToken cancellationToken = default);
    Task<Result<bool>> TryExpireAsync(Guid reservationId, CancellationToken cancellationToken = default);
}