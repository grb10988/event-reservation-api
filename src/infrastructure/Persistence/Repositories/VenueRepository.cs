using Dapper;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Infrastructure.Persistence.Repositories;

internal sealed class VenueRow
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public int Capacity { get; init; }
}

public sealed class VenueRepository(IDbConnectionFactory connectionFactory) : IVenueRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public Task<Result<Venue>> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

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

        }, DatabaseExceptionMapper.Map)
        .Bind(rows => rows.Count == 0
            ? Failure<Venue>(RepositoryErrors.NotFound)
            : Success(Venue.Rehydrate(
                rows[0].Id,
                rows[0].Name,
                rows[0].Address,
                rows[0].Capacity)));

    public Task<Result<IReadOnlyList<Venue>>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Success().MapTry(async Task<IReadOnlyList<Venue>> () =>
        {
            using var connection = _connectionFactory.CreateConnection();

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

        }, DatabaseExceptionMapper.Map);

    public Task<Result<Venue>> AddAsync(Venue venue, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

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

        }, DatabaseExceptionMapper.Map);

    public Task<Result<Venue>> UpdateAsync(Venue venue, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

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

        }, DatabaseExceptionMapper.Map)
        .Bind(rowsAffected => rowsAffected == 1
            ? Success(venue)
            : Failure<Venue>(RepositoryErrors.NotFound));
}