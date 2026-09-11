using Dapper;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using Microsoft.Extensions.Logging;

namespace EventReservation.Infrastructure.Persistence.Repositories;

internal sealed class VenueRow
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public int Capacity { get; init; }
}

public sealed class VenueRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<VenueRepository> logger) : IVenueRepository
{
    public Task<Result<Venue>> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        RepositoryOperations.ExecuteAsync(connectionFactory, logger, async connection =>
        {
            var rows = await connection.QueryAsync<VenueRow>(
                new CommandDefinition(
                    @"
                    SELECT
                        v.id AS Id,
                        v.name AS Name,
                        v.address AS Address,
                        v.capacity AS Capacity
                    FROM
                        venues AS v
                    WHERE
                        v.id = @Id
                    ",
                    new { Id = id },
                    cancellationToken: cancellationToken));

            return rows.ToList();
        })
        .Bind(rows => rows.Count == 0
            ? Failure<Venue>(RepositoryErrors.NotFound)
            : Success(Venue.Rehydrate(
                rows[0].Id,
                rows[0].Name,
                rows[0].Address,
                rows[0].Capacity)));

    public Task<Result<IReadOnlyList<Venue>>> GetAllAsync(CancellationToken cancellationToken = default) =>
        RepositoryOperations.ExecuteAsync(connectionFactory, logger, async Task<IReadOnlyList<Venue>> (connection) =>
        {
            var rows = await connection.QueryAsync<VenueRow>(
                new CommandDefinition(
                    @"
                    SELECT
                        v.id AS Id,
                        v.name AS Name,
                        v.address AS Address,
                        v.capacity AS Capacity
                    FROM
                        venues AS v
                    ",
                    cancellationToken: cancellationToken));

            return rows.Select(r => Venue.Rehydrate(
                r.Id,
                r.Name,
                r.Address,
                r.Capacity)).ToList();
        });

    public Task<Result<Venue>> AddAsync(Venue venue, CancellationToken cancellationToken = default) =>
        RepositoryOperations.ExecuteAsync(connectionFactory, logger, async connection =>
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    INSERT INTO venues
                    (
                        id,
                        name,
                        address,
                        capacity
                    )
                    VALUES
                    (
                        @Id,
                        @Name,
                        @Address,
                        @Capacity
                    )
                    ",
                    new
                    {
                        venue.Id,
                        venue.Name,
                        venue.Address,
                        venue.Capacity
                    },
                    cancellationToken: cancellationToken));

            return venue;
        });

    public Task<Result<Venue>> UpdateAsync(Venue venue, CancellationToken cancellationToken = default) =>
        RepositoryOperations.ExecuteAsync(connectionFactory, logger, async connection =>
        {
            return await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    UPDATE
                        venues
                    SET
                        name = @Name,
                        address = @Address,
                        capacity = @Capacity
                    WHERE
                        id = @Id
                    ",
                    new
                    {
                        venue.Id,
                        venue.Name,
                        venue.Address,
                        venue.Capacity
                    },
                    cancellationToken: cancellationToken));
        })
        .Bind(rowsAffected => rowsAffected == 1
            ? Success(venue)
            : Failure<Venue>(RepositoryErrors.NotFound));
}