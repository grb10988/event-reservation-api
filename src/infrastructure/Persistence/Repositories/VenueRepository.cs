using Dapper;
using EventReservation.Application.Interfaces.Repositories;
using EventReservation.Domain.Models;

namespace EventReservation.Infrastructure.Persistence.Repositories;

internal sealed record VenueRow(Guid Id, string Name, string Address, int Capacity);

public sealed class VenueRepository(IDbConnectionFactory connectionFactory) : IVenueRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public Task<Result<Venue>> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            return await connection.QuerySingleOrDefaultAsync<VenueRow>(
                new CommandDefinition(
                    @"
                    select
                        id
                        , name
                        , address
                        , capacity
                    from venues
                    where id = @Id",
                    new { Id = id },
                    cancellationToken: cancellationToken));
        }, ex => DatabaseExceptionMapper.Map(ex))
        .Bind(row => row is null
            ? Failure<Venue>(RepositoryErrors.NotFound)
            : Success(Venue.Rehydrate(row.Id, row.Name, row.Address, row.Capacity)));

    public Task<Result<IReadOnlyList<Venue>>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Success().MapTry(async Task<IReadOnlyList<Venue>> () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rows = await connection.QueryAsync<VenueRow>(
                new CommandDefinition(
                    @"
                    select
                        id
                        , name
                        , address
                        , capacity
                    from venues",
                    cancellationToken: cancellationToken));

            return rows.Select(r => Venue.Rehydrate(r.Id, r.Name, r.Address, r.Capacity)).ToList();
        }, ex => DatabaseExceptionMapper.Map(ex));

    public Task<Result<Venue>> AddAsync(Venue venue, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    insert into venues (id, name, address, capacity)
                    values (@Id, @Name, @Address, @Capacity)",
                    new { venue.Id, venue.Name, venue.Address, venue.Capacity },
                    cancellationToken: cancellationToken));

            return venue;
        }, ex => DatabaseExceptionMapper.Map(ex));

    public Task<Result<Venue>> UpdateAsync(Venue venue, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            return await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    update venues
                    set
                        name = @Name
                        , address = @Address
                        , capacity = @Capacity
                    where id = @Id",
                    new { venue.Id, venue.Name, venue.Address, venue.Capacity },
                    cancellationToken: cancellationToken));
        }, ex => DatabaseExceptionMapper.Map(ex))
        .Bind(rowsAffected => rowsAffected == 1
            ? Success(venue)
            : Failure<Venue>(RepositoryErrors.NotFound));
}