using EventReservation.Application.Abstractions;
using EventReservation.Application.Interfaces;

namespace EventReservation.Application.Features.Customers;

public sealed record GetCustomerByIdResult(Guid Id, string FirstName, string LastName, string Email);
public sealed record GetCustomerByIdQuery(Guid Id) : IQuery<GetCustomerByIdResult>;

public sealed class GetCustomerByIdQueryHandler(ICustomerRepository customerRepository)
    : IQueryHandler<GetCustomerByIdQuery, GetCustomerByIdResult>
{
    public Task<Result<GetCustomerByIdResult>> HandleAsync(
        GetCustomerByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = customerRepository.GetByIdAsync(query.Id, cancellationToken)
            .Map(customer => new GetCustomerByIdResult(customer.Id, customer.FirstName, customer.LastName, customer.Email));

        return result;
    }
}
