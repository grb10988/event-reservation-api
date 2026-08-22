using EventReservation.Application.Abstractions;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Application.Features.Customers;

public sealed record UpdateCustomerResult(Guid Id, string FirstName, string LastName, string Email);
public sealed record UpdateCustomerCommand(Guid Id, string FirstName, string LastName, string Email) : ICommand<UpdateCustomerResult>;

public sealed class UpdateCustomerCommandHandler(ICustomerRepository customerRepository)
    : ICommandHandler<UpdateCustomerCommand, UpdateCustomerResult>
{
    public Task<Result<UpdateCustomerResult>> HandleAsync(
        UpdateCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = customerRepository.GetByIdAsync(command.Id, cancellationToken)
            .Bind(customer => ValidateCustomer(customer, command))
            .Bind(customer => customerRepository.UpdateAsync(customer, cancellationToken))
            .Map(customer => new UpdateCustomerResult(customer.Id, customer.FirstName, customer.LastName, customer.Email));

        return result;
    }

    private static Result<Customer> ValidateCustomer(Customer customer, UpdateCustomerCommand command)
    {
        var validation = Combine(
            customer.ChangeFirstName(command.FirstName),
            customer.ChangeLastName(command.LastName),
            customer.ChangeEmail(command.Email));

        return validation.IsSuccess
            ? Success(customer)
            : Failure<Customer>(validation.Errors);
    }
}