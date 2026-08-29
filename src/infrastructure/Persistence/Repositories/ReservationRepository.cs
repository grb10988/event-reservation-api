using Dapper;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Infrastructure.Persistence.Repositories;

internal sealed class ReservationRow
{
    public Guid Id { get; init; }
    public Guid SeatId { get; init; }
    public Guid EventId { get; init; }
    public Guid CustomerId { get; init; }
    public decimal Price { get; init; }
    public ReservationStatus Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset HoldExpiresAt { get; init; }
}

public sealed class ReservationRepository(IDbConnectionFactory connectionFactory) : IReservationRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public Task<Result<Reservation>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rows = await connection.QueryAsync<ReservationRow>(
                new CommandDefinition(
                    @"
                    SELECT
                        r.id AS Id,
                        r.seat_id AS SeatId,
                        r.event_id AS EventId,
                        r.customer_id AS CustomerId,
                        r.price AS Price,
                        r.status AS Status,
                        r.created_at AS CreatedAt,
                        r.hold_expires_at AS HoldExpiresAt
                    FROM
                        reservations AS r
                    WHERE
                        r.id = @Id
                    ",
                    new { Id = id },
                    cancellationToken: cancellationToken));

            return rows.ToList();

        }, DatabaseExceptionMapper.Map)
        .Bind(rows => rows.Count == 0
            ? Failure<Reservation>(RepositoryErrors.NotFound)
            : Success(Reservation.Rehydrate(
                rows[0].Id,
                rows[0].SeatId,
                rows[0].EventId,
                rows[0].CustomerId,
                rows[0].Price,
                rows[0].Status,
                rows[0].CreatedAt,
                rows[0].HoldExpiresAt)));

    public Task<Result<IReadOnlyList<Reservation>>> GetByCustomerIdAsync(
        Guid customerId,
        CancellationToken cancellationToken = default) =>
        Success().MapTry(async Task<IReadOnlyList<Reservation>> () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rows = await connection.QueryAsync<ReservationRow>(
                new CommandDefinition(
                    @"
                    SELECT
                        r.id AS Id,
                        r.seat_id AS SeatId,
                        r.event_id AS EventId,
                        r.customer_id AS CustomerId,
                        r.price AS Price,
                        r.status AS Status,
                        r.created_at AS CreatedAt,
                        r.hold_expires_at AS HoldExpiresAt
                    FROM
                        reservations AS r
                    WHERE
                        r.customer_id = @CustomerId
                    ORDER BY
                        r.created_at DESC
                    ",
                    new { CustomerId = customerId },
                    cancellationToken: cancellationToken));

            return rows.Select(r => Reservation.Rehydrate(
                r.Id,
                r.SeatId,
                r.EventId,
                r.CustomerId,
                r.Price,
                r.Status,
                r.CreatedAt,
                r.HoldExpiresAt)).ToList();
        }, DatabaseExceptionMapper.Map);

    public Task<Result<IReadOnlyList<Reservation>>> GetExpiredHoldsAsync(
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default) =>
        Success().MapTry(async Task<IReadOnlyList<Reservation>> () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rows = await connection.QueryAsync<ReservationRow>(
                new CommandDefinition(
                    @"
                    SELECT
                        r.id AS Id,
                        r.seat_id AS SeatId,
                        r.event_id AS EventId,
                        r.customer_id AS CustomerId,
                        r.price AS Price,
                        r.status AS Status,
                        r.created_at AS CreatedAt,
                        r.hold_expires_at AS HoldExpiresAt
                    FROM
                        reservations AS r
                    WHERE
                        r.status = @HeldStatus AND
                        r.hold_expires_at < @AsOf
                    ",
                    new
                    {
                        HeldStatus = ReservationStatus.Held.ToString(),
                        AsOf = asOf
                    },
                    cancellationToken: cancellationToken));

            return rows.Select(r => Reservation.Rehydrate(
                r.Id,
                r.SeatId,
                r.EventId,
                r.CustomerId,
                r.Price,
                r.Status,
                r.CreatedAt,
                r.HoldExpiresAt)).ToList();

        }, DatabaseExceptionMapper.Map);

    public Task<Result<Reservation>> AddAsync(Reservation reservation, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();
        
            await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    INSERT INTO reservations
                    (
                        id,
                        seat_id,
                        event_id,
                        customer_id,
                        price,
                        status,
                        created_at,
                        hold_expires_at
                    )
                    VALUES
                    (
                        @Id,
                        @SeatId,
                        @EventId,
                        @CustomerId,
                        @Price,
                        @Status,
                        @CreatedAt,
                        @HoldExpiresAt
                    )
                    ",
                    new
                    {
                        reservation.Id,
                        reservation.SeatId,
                        reservation.EventId,
                        reservation.CustomerId,
                        reservation.Price,
                        Status = reservation.Status.ToString(),
                        reservation.CreatedAt,
                        reservation.HoldExpiresAt
                    },
                    cancellationToken: cancellationToken));

            return reservation;

        }, DatabaseExceptionMapper.Map);

    public Task<Result<bool>> TryConfirmAsync(Guid reservationId, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    UPDATE
                        reservations
                    SET
                        status = @NewStatus
                    WHERE
                        id = @Id AND
                        status = @RequiredStatus
                    ",
                    new
                    {
                        Id = reservationId,
                        NewStatus = ReservationStatus.Confirmed.ToString(),
                        RequiredStatus = ReservationStatus.Held.ToString()
                    },
                    cancellationToken: cancellationToken));

            return rowsAffected == 1;

        }, DatabaseExceptionMapper.Map);

    public Task<Result<bool>> TryCancelAsync(Guid reservationId, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    UPDATE
                        reservations
                    SET
                        status = @NewStatus
                    WHERE
                        id = @Id AND
                        status = ANY(@RequiredStatus)
                    ",
                    new
                    {
                        Id = reservationId,
                        NewStatus = ReservationStatus.Cancelled.ToString(),
                        RequiredStatus = new[]
                        {
                            ReservationStatus.Held.ToString(),
                            ReservationStatus.Confirmed.ToString()
                        }
                    },
                    cancellationToken: cancellationToken));

            return rowsAffected == 1;

        }, DatabaseExceptionMapper.Map);

    public Task<Result<bool>> TryExpireAsync(Guid reservationId, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    UPDATE
                        reservations
                    SET
                        status = @NewStatus
                    WHERE
                        id = @Id AND
                        status = @RequiredStatus
                    ",
                    new
                    {
                        Id = reservationId,
                        NewStatus = ReservationStatus.Expired.ToString(),
                        RequiredStatus = ReservationStatus.Held.ToString()
                    },
                    cancellationToken: cancellationToken));

            return rowsAffected == 1;

        }, DatabaseExceptionMapper.Map);
}