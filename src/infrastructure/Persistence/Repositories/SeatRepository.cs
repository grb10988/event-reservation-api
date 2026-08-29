using Dapper;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Infrastructure.Persistence.Repositories;

internal sealed class SeatRow
{
    public Guid Id { get; init; }
    public Guid VenueId { get; init; }
    public string Section { get; init; } = string.Empty;
    public int Row { get; init; }
    public int Number { get; init; }
    public SeatStatus Status { get; init; }
}

public sealed class SeatRepository(IDbConnectionFactory connectionFactory) : ISeatRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public Task<Result<Seat>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rows = await connection.QueryAsync<SeatRow>(
                new CommandDefinition(
                    @"
                    SELECT
                        s.id AS Id,
                        s.venue_id AS VenueId,
                        s.section AS Section,
                        s.row AS Row,
                        s.number AS Number,
                        s.status AS Status
                    FROM
                        seats AS s
                    WHERE
                        s.id = @Id
                    ",
                    new { Id = id },
                    cancellationToken: cancellationToken));

            return rows.ToList();

        }, DatabaseExceptionMapper.Map)
        .Bind(rows => rows.Count == 0
            ? Failure<Seat>(RepositoryErrors.NotFound)
            : Success(Seat.Rehydrate(
                rows[0].Id,
                rows[0].VenueId,
                rows[0].Section,
                rows[0].Row,
                rows[0].Number,
                rows[0].Status)));

    public Task<Result<IReadOnlyList<Seat>>> GetByVenueIdAsync(Guid venueId, CancellationToken cancellationToken = default) =>
        Success().MapTry(async Task<IReadOnlyList<Seat>> () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rows = await connection.QueryAsync<SeatRow>(
                new CommandDefinition(
                    @"
                    SELECT
                        s.id AS Id,
                        s.venue_id AS VenueId,
                        s.section AS Section,
                        s.row AS Row,
                        s.number AS Number,
                        s.status AS Status
                    FROM
                        seats AS s
                    WHERE
                        s.venue_id = @VenueId
                    ORDER BY
                        s.section,
                        s.row,
                        s.number
                    ",
                    new { VenueId = venueId },
                    cancellationToken: cancellationToken));

            return rows.Select(r => Seat.Rehydrate(
                r.Id,
                r.VenueId,
                r.Section,
                r.Row,
                r.Number,
                r.Status)).ToList();

        }, DatabaseExceptionMapper.Map);

    public Task<Result<Seat>> AddAsync(Seat seat, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    INSERT INTO seats
                    (
                        id,
                        venue_id,
                        section,
                        row,
                        number,
                        status
                    )
                    VALUES
                    (
                        @Id,
                        @VenueId,
                        @Section,
                        @Row,
                        @Number,
                        @Status
                    )
                    ",
                    new
                    {
                        seat.Id,
                        seat.VenueId,
                        seat.Section,
                        seat.Row,
                        seat.Number,
                        Status = seat.Status.ToString()
                    },
                    cancellationToken: cancellationToken));

            return seat;

        }, DatabaseExceptionMapper.Map);

    public Task<Result<bool>> TryHoldAsync(Guid seatId, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    UPDATE
                        seats
                    SET
                        status = @NewStatus
                    WHERE
                        id = @Id AND
                        status = @RequiredStatus
                    ",
                    new
                    {
                        Id = seatId,
                        NewStatus = SeatStatus.Held.ToString(),
                        RequiredStatus = SeatStatus.Available.ToString()
                    },
                    cancellationToken: cancellationToken));

            return rowsAffected == 1;

        }, DatabaseExceptionMapper.Map);

    public Task<Result<bool>> TryReserveAsync(Guid seatId, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    UPDATE
                        seats
                    SET
                        status = @NewStatus
                    WHERE
                        id = @Id AND
                        status = @RequiredStatus
                    ",
                    new
                    {
                        Id = seatId,
                        NewStatus = SeatStatus.Reserved.ToString(),
                        RequiredStatus = SeatStatus.Held.ToString()
                    },
                    cancellationToken: cancellationToken));

            return rowsAffected == 1;

        }, DatabaseExceptionMapper.Map);

    public Task<Result<bool>> TryReleaseAsync(Guid seatId, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    UPDATE
                        seats
                    SET
                        status = @NewStatus
                    WHERE
                        id = @Id AND
                        status = ANY(@RequiredStatus)
                    ",
                    new
                    {
                        Id = seatId,
                        NewStatus = SeatStatus.Available.ToString(),
                        RequiredStatus = new[]
                        {
                            SeatStatus.Held.ToString(),
                            SeatStatus.Reserved.ToString()
                        }
                    },
                    cancellationToken: cancellationToken));

            return rowsAffected == 1;

        }, DatabaseExceptionMapper.Map);
}