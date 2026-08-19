using Dapper;
using EventReservation.Application.Interfaces.Repositories;
using EventReservation.Domain.Models;

namespace EventReservation.Infrastructure.Persistence.Repositories;

internal sealed record ReservationRow(
    Guid Id,
    Guid SeatId,
    Guid EventId,
    Guid CustomerId,
    decimal Price,
    ReservationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset HoldExpiresAt);

public sealed class ReservationRepository(IDbConnectionFactory connectionFactory) : IReservationRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public Task<Result<Reservation>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            return await connection.QuerySingleOrDefaultAsync<ReservationRow>(
                new CommandDefinition(
                    @"
                    select
                        id
                        , seat_id
                        , event_id
                        , customer_id
                        , price
                        , status
                        , created_at
                        , hold_expires_at
                    from reservations
                    where id = @Id",
                    new { Id = id },
                    cancellationToken: cancellationToken));
        }, ex => DatabaseExceptionMapper.Map(ex))
        .Bind(row => row is null
            ? Failure<Reservation>(RepositoryErrors.NotFound)
            : Success(Reservation.Rehydrate(
                row.Id,
                row.SeatId,
                row.EventId,
                row.CustomerId,
                row.Price,
                row.Status,
                row.CreatedAt,
                row.HoldExpiresAt)));

    public Task<Result<IReadOnlyList<Reservation>>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        Success().MapTry(async Task<IReadOnlyList<Reservation>> () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rows = await connection.QueryAsync<ReservationRow>(
                new CommandDefinition(
                    @"
                    select
                        id
                        , seat_id
                        , event_id
                        , customer_id
                        , price
                        , status
                        , created_at
                        , hold_expires_at
                    from reservations
                    where customer_id = @CustomerId
                    order by created_at desc",
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
        }, ex => DatabaseExceptionMapper.Map(ex));

    public Task<Result<IReadOnlyList<Reservation>>> GetExpiredHoldsAsync(DateTimeOffset asOf, CancellationToken cancellationToken = default) =>
        Success().MapTry(async Task<IReadOnlyList<Reservation>> () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rows = await connection.QueryAsync<ReservationRow>(
                new CommandDefinition(
                    @"
                    select
                        id
                        , seat_id
                        , event_id
                        , customer_id
                        , price
                        , status
                        , created_at
                        , hold_expires_at
                    from reservations
                    where status = @HeldStatus
                        and hold_expires_at < @AsOf",
                    new
                    {
                        HeldStatus = ReservationStatus.Held,
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
        }, ex => DatabaseExceptionMapper.Map(ex));

    public Task<Result<Reservation>> AddAsync(Reservation reservation, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    insert into reservations(id, seat_id, event_id, customer_id, price, status, created_at, hold_expires_at)
                    values (@Id, @SeatId, @EventId, @CustomerId, @Price, @Status, @CreatedAt, @HoldExpiresAt)",
                    new
                    {
                        reservation.Id,
                        reservation.SeatId,
                        reservation.EventId,
                        reservation.CustomerId,
                        reservation.Price,
                        reservation.Status,
                        reservation.CreatedAt,
                        reservation.HoldExpiresAt
                    },
                    cancellationToken: cancellationToken));

            return reservation;
        }, ex => DatabaseExceptionMapper.Map(ex));

    public Task<Result<bool>> TryConfirmAsync(Guid reservationId, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    update reservations
                    set status = @NewStatus
                    where id = @Id
                        and status in @RequiredStatuses",
                    new
                    {
                        Id = reservationId,
                        NewStatus = ReservationStatus.Confirmed,
                        RequiredStatus = ReservationStatus.Held
                    },
                    cancellationToken: cancellationToken));

            return rowsAffected == 1;
        }, ex => DatabaseExceptionMapper.Map(ex));

    public Task<Result<bool>> TryCancelAsync(Guid reservationId, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    update reservations
                    set status = @NewStatus
                    where id = @Id
                        and status in @RequiredStatuses",
                    new
                    {
                        Id = reservationId,
                        NewStatus = ReservationStatus.Cancelled,
                        RequiredStatus = new[] { ReservationStatus.Held, ReservationStatus.Confirmed }
                    },
                    cancellationToken: cancellationToken));

            return rowsAffected == 1;
        }, ex => DatabaseExceptionMapper.Map(ex));

    public Task<Result<bool>> TryExpireAsync(Guid reservationId, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    update reservations
                    set status = @NewStatus
                    where id = @Id
                        and status in @RequiredStatuses",
                    new
                    {
                        Id = reservationId,
                        NewStatus = ReservationStatus.Expired,
                        RequiredStatus = ReservationStatus.Held
                    },
                    cancellationToken: cancellationToken));

            return rowsAffected == 1;
        }, ex => DatabaseExceptionMapper.Map(ex));
}