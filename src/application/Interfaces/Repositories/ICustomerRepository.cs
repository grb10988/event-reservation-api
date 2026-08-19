using EventReservation.Domain.Models;

namespace EventReservation.Application.Interfaces.Repositories;

public interface ICustomerRepository
{
    Task<Result<Customer>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<Customer>> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<Result<Customer>> AddAsync(Customer customer, CancellationToken cancellationToken = default);
    Task<Result<Customer>> UpdateAsync(Customer customer, CancellationToken cancellationToken = default);
}