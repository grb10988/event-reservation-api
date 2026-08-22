using EventReservation.Application.Abstractions;
using EventReservation.Application.Interfaces;

namespace EventReservation.Application.Features.Customers;

public sealed record GetCustomerByEmailResult(Guid Id, string FirstName, string LastName, string Email);
public sealed record GetCustomerByEmailQuery(string Email) : IQuery<GetCustomerByEmailResult>;

public sealed class GetCustomerByEmailQueryHandler(ICustomerRepository customerRepository)
    : IQueryHandler<GetCustomerByEmailQuery, GetCustomerByEmailResult>
{
    public Task<Result<GetCustomerByEmailResult>> HandleAsync(
        GetCustomerByEmailQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = customerRepository.GetByEmailAsync(query.Email, cancellationToken)
            .Map(customer => new GetCustomerByEmailResult(customer.Id, customer.FirstName, customer.LastName, customer.Email));

        return result;
    }
}