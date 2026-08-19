using EventReservation.Domain.Models;

namespace EventReservation.Application.Interfaces.Repositories;

public interface IOrderRepository
{
    Task<Result<Order>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<Order>> GetByConfirmationNumberAsync(string confirmationNumber, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<Order>>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<Result<Order>> AddAsync(Order order, CancellationToken cancellationToken = default);
    Task<Result<bool>> TryCompleteAsync(Guid orderId, string confirmationNumber, CancellationToken cancellationToken = default);
    Task<Result<bool>> TryCancelAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<Result<bool>> TryRefundAsync(Guid orderId, CancellationToken cancellationToken = default);
}