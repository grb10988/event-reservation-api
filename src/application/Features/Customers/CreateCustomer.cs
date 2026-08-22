using EventReservation.Application.Abstractions;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Application.Features.Customers;

public sealed record CreateCustomerResult(Guid Id, string FirstName, string LastName, string Email);
public sealed record CreateCustomerCommand(string FirstName, string LastName, string Email)
    : ICommand<CreateCustomerResult>;

public sealed class CreateCustomerHandler(ICustomerRepository customerRepository)
    : ICommandHandler<CreateCustomerCommand, CreateCustomerResult>
{
    public Task<Result<CreateCustomerResult>> HandleAsync(
        CreateCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = Customer.Create(command.FirstName, command.LastName, command.Email)
            .Bind(customer => customerRepository.AddAsync(customer, cancellationToken))
            .Map(customer => new CreateCustomerResult(customer.Id, customer.FirstName, customer.LastName, customer.Email));

        return result;
    }
}