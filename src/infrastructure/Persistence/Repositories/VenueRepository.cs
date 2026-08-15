using Dapper;
using EventReservation.Application.Interfaces.Repositories;
using EventReservation.Domain.Models;

namespace EventReservation.Infrastructure.Persistence.Repositories;

internal sealed record VenueRow(Guid Id, string Name, string Address, int Capacity);

public sealed class VenueRepository(IDbConnectionFactory connectionFactory) : IVenueRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task<Venue?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<VenueRow>(
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

        return row is null
            ? null
            : Venue.Rehydrate(row.Id, row.Name, row.Address, row.Capacity);
    }

    public async Task<IReadOnlyList<Venue>> GetAllAsync(CancellationToken cancellationToken)
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
    }

    public async Task AddAsync(Venue venue, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            new CommandDefinition(
                @"
                insert into venues (id, name, address, capacity)
                values (@Id, @Name, @Address, @Capacity)",
                new { venue.Id, venue.Name, venue.Address, venue.Capacity },
                cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(Venue venue, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
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
    }
}