using Dapper;
using EventReservation.Application.Interfaces.Repositories;
using EventReservation.Domain.Models;

namespace EventReservation.Infrastructure.Persistence.Repositories;

internal sealed record CustomerRow(Guid Id, string FirstName, string LastName, string Email);

public sealed class CustomerRepository(IDbConnectionFactory connectionFactory) : ICustomerRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public Task<Result<Customer>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            return await connection.QuerySingleOrDefaultAsync<CustomerRow>(
                new CommandDefinition(
                    @"
                    select
                        id
                        , first_name
                        , last_name
                        , email
                    from customers
                    where id = @Id",
                    new { Id = id },
                    cancellationToken: cancellationToken));

        }, ex => DatabaseExceptionMapper.Map(ex))
        .Bind(row => row is null
            ? Failure<Customer>(RepositoryErrors.NotFound)
            : Success(Customer.Rehydrate(
                row.Id,
                row.FirstName,
                row.LastName,
                row.Email)));

    public Task<Result<Customer>> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            return await connection.QuerySingleOrDefaultAsync<CustomerRow>(
                new CommandDefinition(
                    @"
                    select
                        id
                        , first_name
                        , last_name
                        , email
                    from customers
                    where email = @Email",
                    new { Email = email },
                    cancellationToken: cancellationToken));
        }, ex => DatabaseExceptionMapper.Map(ex))
        .Bind(row => row is null
            ? Failure<Customer>(RepositoryErrors.NotFound)
            : Success(Customer.Rehydrate(
                row.Id,
                row.FirstName,
                row.LastName,
                row.Email)));

    public Task<Result<Customer>> AddAsync(Customer customer, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    insert into customers(id, first_name, last_name, email)
                    values (@Id, @FirstName, @LastName, @Email)",
                    new
                    {
                        customer.Id,
                        customer.FirstName,
                        customer.LastName,
                        customer.Email
                    },
                    cancellationToken: cancellationToken));

            return customer;
        }, ex => DatabaseExceptionMapper.Map(ex));

    public Task<Result<Customer>> UpdateAsync(Customer customer, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            return await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    update customers
                    set first_name = @FirstName
                        , last_name = @LastName
                        , email = @Email
                    where id = @Id",
                    new
                    {
                        customer.Id,
                        customer.FirstName,
                        customer.LastName,
                        customer.Email
                    },
                    cancellationToken: cancellationToken));

        }, ex => DatabaseExceptionMapper.Map(ex))
        .Bind(rowsAffected => rowsAffected == 1
            ? Success(customer)
            : Failure<Customer>(RepositoryErrors.NotFound));
}