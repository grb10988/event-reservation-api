using System.Data;
using Dapper;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Infrastructure.Persistence.Repositories;

internal sealed class OrderRow
{
    public Guid Id { get; init; }
    public Guid CustomerId { get; init; }
    public OrderStatus Status { get; init; }
    public string? ConfirmationNumber { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

internal sealed class OrderWithReservationRow
{
    public Guid Id { get; init; }
    public Guid CustomerId { get; init; }
    public OrderStatus Status { get; init; }
    public string? ConfirmationNumber { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public Guid? ReservationId { get; init; }
}

public sealed class OrderRepository(IDbConnectionFactory connectionFactory) : IOrderRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    private static async Task<List<Guid>> LoadReservationIdsAsync(
        IDbConnection connection,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var reservationIds = await connection.QueryAsync<Guid>(
            new CommandDefinition(
                @"
                SELECT
                    orr.reservation_id AS ReservationId
                FROM
                    order_reservations AS orr
                WHERE
                    orr.order_id = @OrderId
                ",
                new { OrderId = orderId },
                cancellationToken: cancellationToken));

        return reservationIds.ToList();
    }

    public Task<Result<Order>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rows = (await connection.QueryAsync<OrderRow>(
                new CommandDefinition(
                    @"
                    SELECT
                        o.id AS Id,
                        o.customer_id AS CustomerId,
                        o.status AS Status,
                        o.confirmation_number AS ConfirmationNumber,
                        o.created_at AS CreatedAt
                    FROM
                        orders AS o
                    WHERE
                        o.id = @Id
                    ",
                    new { Id = id },
                    cancellationToken: cancellationToken))).ToList();

            if (rows.Count == 0)
                return (Rows: rows, ReservationIds: new List<Guid>());

            var reservationIds = await LoadReservationIdsAsync(connection, rows[0].Id, cancellationToken);

            return (Rows: rows, ReservationIds: reservationIds);

        }, DatabaseExceptionMapper.Map)
        .Bind(result => result.Rows.Count == 0
            ? Failure<Order>(RepositoryErrors.NotFound)
            : Success(Order.Rehydrate(
                result.Rows[0].Id,
                result.Rows[0].CustomerId,
                result.ReservationIds,
                result.Rows[0].Status,
                result.Rows[0].ConfirmationNumber,
                result.Rows[0].CreatedAt)));

    public Task<Result<Order>> GetByConfirmationNumberAsync(string confirmationNumber, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rows = (await connection.QueryAsync<OrderRow>(
                new CommandDefinition(
                    @"
                    SELECT
                        o.id AS Id,
                        o.customer_id AS CustomerId,
                        o.status AS Status,
                        o.confirmation_number AS ConfirmationNumber,
                        o.created_at AS CreatedAt
                    FROM
                        orders AS o
                    WHERE
                        o.confirmation_number = @ConfirmationNumber
                    ",
                    new { ConfirmationNumber = confirmationNumber },
                    cancellationToken: cancellationToken))).ToList();

            if (rows.Count == 0)
                return (Rows: rows, ReservationIds: new List<Guid>());

            var reservationIds = await LoadReservationIdsAsync(connection, rows[0].Id, cancellationToken);

            return (Rows: rows, ReservationIds: reservationIds);

        }, DatabaseExceptionMapper.Map)
        .Bind(result => result.Rows.Count == 0
            ? Failure<Order>(RepositoryErrors.NotFound)
            : Success(Order.Rehydrate(
                result.Rows[0].Id,
                result.Rows[0].CustomerId,
                result.ReservationIds,
                result.Rows[0].Status,
                result.Rows[0].ConfirmationNumber,
                result.Rows[0].CreatedAt)));

    public Task<Result<IReadOnlyList<Order>>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        Success().MapTry(async Task<IReadOnlyList<Order>> () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rows = await connection.QueryAsync<OrderWithReservationRow>(
                new CommandDefinition(
                    @"
                    SELECT
                        o.id AS Id,
                        o.customer_id AS CustomerId,
                        o.status AS Status,
                        o.confirmation_number AS ConfirmationNumber,
                        o.created_at AS CreatedAt,
                        orr.reservation_id AS ReservationId
                    FROM
                        orders AS o
                    LEFT JOIN
                        order_reservations AS orr
                            ON orr.order_id = o.id
                    WHERE
                        o.customer_id = @CustomerId
                    ORDER BY
                        o.created_at DESC
                    ",
                    new { CustomerId = customerId },
                    cancellationToken: cancellationToken));

            return rows
                .GroupBy(r => new
                {
                    r.Id,
                    r.CustomerId,
                    r.Status,
                    r.ConfirmationNumber,
                    r.CreatedAt
                })
                .Select(g => Order.Rehydrate(
                    g.Key.Id,
                    g.Key.CustomerId,
                    g.SelectMany(r =>
                        r.ReservationId is Guid reservationId
                            ? new[] { reservationId }
                            : Array.Empty<Guid>()).ToList(),
                    g.Key.Status,
                    g.Key.ConfirmationNumber,
                    g.Key.CreatedAt))
                .ToList();

        }, DatabaseExceptionMapper.Map);

    public Task<Result<Order>> AddAsync(Order order, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    INSERT INTO orders
                    (
                        id,
                        customer_id,
                        status,
                        confirmation_number,
                        created_at
                    )
                    VALUES
                    (
                        @Id,
                        @CustomerId,
                        @Status,
                        @ConfirmationNumber,
                        @CreatedAt
                    )
                    ",
                    new
                    {
                        order.Id,
                        order.CustomerId,
                        Status = order.Status.ToString(),
                        order.ConfirmationNumber,
                        order.CreatedAt
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken));

            await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    INSERT INTO order_reservations
                    (
                        order_id,
                        reservation_id
                    )
                    SELECT
                        @OrderId,
                        unnest(@ReservationIds::uuid[])
                    ",
                    new
                    {
                        OrderId = order.Id,
                        ReservationIds = order.ReservationIds.ToArray()
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken));

            transaction.Commit();

            return order;

        }, DatabaseExceptionMapper.Map);

    public Task<Result<bool>> TryCompleteAsync(Guid orderId, string confirmationNumber, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    UPDATE
                        orders
                    SET
                        status = @NewStatus,
                        confirmation_number = @ConfirmationNumber
                    WHERE
                        id = @Id AND
                        status = @RequiredStatus
                    ",
                    new
                    {
                        Id = orderId,
                        NewStatus = OrderStatus.Completed.ToString(),
                        ConfirmationNumber = confirmationNumber,
                        RequiredStatus = OrderStatus.Pending.ToString()
                    },
                    cancellationToken: cancellationToken));

            return rowsAffected == 1;

        }, DatabaseExceptionMapper.Map);

    public Task<Result<bool>> TryCancelAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    UPDATE
                        orders
                    SET
                        status = @NewStatus
                    WHERE
                        id = @Id AND
                        status = @RequiredStatus
                    ",
                    new
                    {
                        Id = orderId,
                        NewStatus = OrderStatus.Cancelled.ToString(),
                        RequiredStatus = OrderStatus.Pending.ToString()
                    },
                    cancellationToken: cancellationToken));

            return rowsAffected == 1;

        }, DatabaseExceptionMapper.Map);

    public Task<Result<bool>> TryRefundAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    UPDATE
                        orders
                    SET
                        status = @NewStatus
                    WHERE
                        id = @Id AND
                        status = @RequiredStatus
                    ",
                    new
                    {
                        Id = orderId,
                        NewStatus = OrderStatus.Refunded.ToString(),
                        RequiredStatus = OrderStatus.Completed.ToString()
                    },
                    cancellationToken: cancellationToken));

            return rowsAffected == 1;

        }, DatabaseExceptionMapper.Map);
}