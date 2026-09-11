using Dapper;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using Microsoft.Extensions.Logging;

namespace EventReservation.Infrastructure.Persistence.Repositories;

internal sealed class CustomerRow
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}

public sealed class CustomerRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<CustomerRepository> logger) : ICustomerRepository
{
    public Task<Result<Customer>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        RepositoryOperations.ExecuteAsync(connectionFactory, logger, async connection =>
        {
            var rows = await connection.QueryAsync<CustomerRow>(
                new CommandDefinition(
                    @"
                    SELECT
                        c.id AS Id,
                        c.first_name AS FirstName,
                        c.last_name AS LastName,
                        c.email AS Email
                    FROM
                        customers AS c
                    WHERE
                        c.id = @Id
                    ",
                    new { Id = id },
                    cancellationToken: cancellationToken));

            return rows.ToList();
        })
        .Bind(rows => rows.Count == 0
            ? Failure<Customer>(RepositoryErrors.NotFound)
            : Success(Customer.Rehydrate(
                rows[0].Id,
                rows[0].FirstName,
                rows[0].LastName,
                rows[0].Email)));

    public Task<Result<Customer>> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        RepositoryOperations.ExecuteAsync(connectionFactory, logger, async connection =>
        {
            var rows = await connection.QueryAsync<CustomerRow>(
                new CommandDefinition(
                    @"
                    SELECT
                        c.id AS Id,
                        c.first_name AS FirstName,
                        c.last_name AS LastName,
                        c.email AS Email
                    FROM
                        customers AS c
                    WHERE
                        c.email = @Email
                    ",
                    new { Email = email },
                    cancellationToken: cancellationToken));

            return rows.ToList();
        })
        .Bind(rows => rows.Count == 0
            ? Failure<Customer>(RepositoryErrors.NotFound)
            : Success(Customer.Rehydrate(
                rows[0].Id,
                rows[0].FirstName,
                rows[0].LastName,
                rows[0].Email)));

    public Task<Result<Customer>> AddAsync(Customer customer, CancellationToken cancellationToken = default) =>
        RepositoryOperations.ExecuteAsync(connectionFactory, logger, async connection =>
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    INSERT INTO customers
                    (
                        id,
                        first_name,
                        last_name,
                        email
                    )
                    VALUES
                    (
                        @Id,
                        @FirstName,
                        @LastName,
                        @Email
                    )
                    ",
                    new
                    {
                        customer.Id,
                        customer.FirstName,
                        customer.LastName,
                        customer.Email
                    },
                    cancellationToken: cancellationToken));

            return customer;
        });

    public Task<Result<Customer>> UpdateAsync(Customer customer, CancellationToken cancellationToken = default) =>
        RepositoryOperations.ExecuteAsync(connectionFactory, logger, async connection =>
        {
            return await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    UPDATE
                        customers
                    SET
                        first_name = @FirstName,
                        last_name = @LastName,
                        email = @Email
                    WHERE
                        id = @Id
                    ",
                    new
                    {
                        customer.Id,
                        customer.FirstName,
                        customer.LastName,
                        customer.Email
                    },
                    cancellationToken: cancellationToken));
        })
        .Bind(rowsAffected => rowsAffected == 1
            ? Success(customer)
            : Failure<Customer>(RepositoryErrors.NotFound));
}