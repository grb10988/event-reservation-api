using Dapper;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Infrastructure.Persistence.Repositories;

internal sealed record SeatRow(Guid Id, Guid VenueId, string Section, int Row, int Number, SeatStatus Status);

public sealed class SeatRepository(IDbConnectionFactory connectionFactory) : ISeatRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public Task<Result<Seat>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            return await connection.QuerySingleOrDefaultAsync<SeatRow>(
                new CommandDefinition(
                    @"
                    select
                        id
                        , venue_id
                        , section
                        , row
                        , number
                        , status
                    from seats
                    where id = @Id",
                    new { Id = id },
                    cancellationToken: cancellationToken));

        }, ex => DatabaseExceptionMapper.Map(ex))
        .Bind(row => row is null
            ? Failure<Seat>(RepositoryErrors.NotFound)
            : Success(Seat.Rehydrate(row.Id, row.VenueId, row.Section, row.Row, row.Number, row.Status)));

    public Task<Result<IReadOnlyList<Seat>>> GetByVenueIdAsync(Guid venueId, CancellationToken cancellationToken = default) =>
        Success().MapTry(async Task<IReadOnlyList<Seat>> () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rows = await connection.QueryAsync<SeatRow>(
                new CommandDefinition(
                    @"
                    select
                        id
                        , venue_id
                        , section
                        , row
                        , number
                        , status
                    from seats
                    where venue_id = @VenueId
                    order by section, row, number",
                    new { VenueId = venueId },
                    cancellationToken: cancellationToken));

            return rows.Select(r => Seat.Rehydrate(r.Id, r.VenueId, r.Section, r.Row, r.Number, r.Status)).ToList();
        }, ex => DatabaseExceptionMapper.Map(ex));

    public Task<Result<Seat>> AddAsync(Seat seat, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    insert into seats (id, venue_id, section, row, number, status)
                    values (@Id, @VenueId, @Section, @Row, @Number, @Status)",
                    new { seat.Id, seat.VenueId, seat.Section, seat.Row, seat.Number, seat.Status },
                    cancellationToken: cancellationToken));

            return seat;
        }, ex => DatabaseExceptionMapper.Map(ex));

    public Task<Result<bool>> TryHoldAsync(Guid seatId, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    update seats
                    set status = @NewStatus
                    where id = @Id
                        and status = @RequiredStatus",
                    new { Id = seatId, NewStatus = SeatStatus.Held, RequiredStatus = SeatStatus.Available },
                    cancellationToken: cancellationToken));

            return rowsAffected == 1;
        }, ex => DatabaseExceptionMapper.Map(ex));

    public Task<Result<bool>> TryReserveAsync(Guid seatId, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    update seats
                    set status = @NewStatus
                    where id = @Id
                        and status = @RequiredStatus",
                    new { Id = seatId, NewStatus = SeatStatus.Reserved, RequiredStatus = SeatStatus.Held },
                    cancellationToken: cancellationToken));

            return rowsAffected == 1;
        }, ex => DatabaseExceptionMapper.Map(ex));

    public Task<Result<bool>> TryReleaseAsync(Guid seatId, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    update seats
                    set status = @NewStatus
                    where id = @Id
                        and status in @RequiredStatus",
                    new
                    {
                        Id = seatId,
                        NewStatus = SeatStatus.Available,
                        RequiredStatus = new[] { SeatStatus.Held, SeatStatus.Reserved }
                    },
                    cancellationToken: cancellationToken));

            return rowsAffected == 1;
        }, ex => DatabaseExceptionMapper.Map(ex));
}